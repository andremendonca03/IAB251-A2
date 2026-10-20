using AeroLink.Models;
using ClosedXML.Excel;

namespace AeroLink.Data;

public sealed class ManifestImport
{
    public required List<Flight> Flights { get; init; }
    public required List<Bag> Bags { get; init; }
    public required List<BaggageException> Exceptions { get; init; }
}

public sealed class ManifestFormatException : Exception
{
    public ManifestFormatException(string message) : base(message) { }
}

public static class ManifestWorkbook
{
    private static readonly string[] FlightColumns =
    [
        "FlightId", "FlightLabel", "FlightNumber", "DepartureAirport", "ArrivalAirport",
        "ScheduledDepartureUtc", "AircraftId", "LoadingStatus"
    ];

    private static readonly string[] BagColumns =
    [
        "BagId", "FlightId", "Tag", "HandlingType", "HandlingInstruction", "Outcome"
    ];

    private static readonly string[] ExceptionColumns =
    [
        "ExceptionId", "BagId", "FlightId", "Category", "Description", "Status", "DecisionReason"
    ];

    public static ManifestImport Read(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(buffer);
        }
        catch (Exception)
        {
            throw new ManifestFormatException("The file is not a valid .xlsx workbook.");
        }

        using (workbook)
        {
            var flights = ReadFlights(Sheet(workbook, "Flights"));
            var bags = ReadBags(Sheet(workbook, "Bags"));
            var exceptions = ReadExceptions(Sheet(workbook, "Exceptions"));
            return new ManifestImport { Flights = flights, Bags = bags, Exceptions = exceptions };
        }
    }

    private static IXLWorksheet Sheet(XLWorkbook workbook, string name)
    {
        var sheet = workbook.Worksheets.FirstOrDefault(worksheet => worksheet.Name == name);
        if (sheet is null)
        {
            throw new ManifestFormatException($"The workbook is missing the {name} sheet.");
        }

        return sheet;
    }

    private static Dictionary<string, int> HeaderMap(IXLWorksheet sheet, string[] required)
    {
        var header = sheet.Row(1);
        var lastColumn = header.LastCellUsed()?.Address.ColumnNumber ?? 0;
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var column = 1; column <= lastColumn; column++)
        {
            var name = header.Cell(column).GetString().Trim();
            if (name.Length > 0)
            {
                map[name] = column;
            }
        }

        var missing = required.Where(name => !map.ContainsKey(name)).ToArray();
        if (missing.Length > 0)
        {
            throw new ManifestFormatException(
                $"{sheet.Name} is missing columns: {string.Join(", ", missing)}.");
        }

        return map;
    }

    private static List<Flight> ReadFlights(IXLWorksheet sheet)
    {
        var columns = HeaderMap(sheet, FlightColumns);
        var flights = new List<Flight>();
        var ids = new HashSet<int>();

        foreach (var row in DataRows(sheet, columns))
        {
            var flight = new Flight
            {
                FlightId = ReadInt(row, columns, "FlightId"),
                FlightLabel = ReadRequiredText(row, columns, "FlightLabel"),
                FlightNumber = ReadRequiredText(row, columns, "FlightNumber"),
                DepartureAirport = ReadRequiredText(row, columns, "DepartureAirport"),
                ArrivalAirport = ReadRequiredText(row, columns, "ArrivalAirport"),
                ScheduledDepartureUtc = ReadDate(row, columns),
                AircraftId = ReadRequiredText(row, columns, "AircraftId"),
                LoadingStatus = ReadRequiredText(row, columns, "LoadingStatus")
            };

            if (!ids.Add(flight.FlightId))
            {
                throw new ManifestFormatException($"Flights has a duplicate FlightId {flight.FlightId}.");
            }

            flights.Add(flight);
        }

        return flights;
    }

    private static List<Bag> ReadBags(IXLWorksheet sheet)
    {
        var columns = HeaderMap(sheet, BagColumns);
        var bags = new List<Bag>();
        var ids = new HashSet<int>();

        foreach (var row in DataRows(sheet, columns))
        {
            var bag = new Bag
            {
                BagId = ReadInt(row, columns, "BagId"),
                FlightId = ReadInt(row, columns, "FlightId"),
                Tag = ReadRequiredText(row, columns, "Tag"),
                HandlingType = ReadRequiredText(row, columns, "HandlingType"),
                HandlingInstruction = ReadRequiredText(row, columns, "HandlingInstruction"),
                Outcome = ReadRequiredText(row, columns, "Outcome")
            };

            if (!ids.Add(bag.BagId))
            {
                throw new ManifestFormatException($"Bags has a duplicate BagId {bag.BagId}.");
            }

            bags.Add(bag);
        }

        return bags;
    }

    private static List<BaggageException> ReadExceptions(IXLWorksheet sheet)
    {
        var columns = HeaderMap(sheet, ExceptionColumns);
        var exceptions = new List<BaggageException>();
        var ids = new HashSet<int>();

        foreach (var row in DataRows(sheet, columns))
        {
            var item = new BaggageException
            {
                ExceptionId = ReadInt(row, columns, "ExceptionId"),
                BagId = ReadInt(row, columns, "BagId"),
                FlightId = ReadInt(row, columns, "FlightId"),
                Category = ReadRequiredText(row, columns, "Category"),
                Description = ReadRequiredText(row, columns, "Description"),
                Status = ReadRequiredText(row, columns, "Status"),
                DecisionReason = ReadText(row, columns, "DecisionReason")
            };

            if (!ids.Add(item.ExceptionId))
            {
                throw new ManifestFormatException($"Exceptions has a duplicate ExceptionId {item.ExceptionId}.");
            }

            exceptions.Add(item);
        }

        return exceptions;
    }

    private static IEnumerable<IXLRow> DataRows(IXLWorksheet sheet, IReadOnlyDictionary<string, int> columns)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            if (columns.Values.Any(column => !row.Cell(column).IsEmpty()))
            {
                yield return row;
            }
        }
    }

    private static int ReadInt(IXLRow row, IReadOnlyDictionary<string, int> columns, string name)
    {
        var cell = row.Cell(columns[name]);
        if (cell.IsEmpty())
        {
            throw new ManifestFormatException($"{row.Worksheet.Name} row {row.RowNumber()} is missing {name}.");
        }

        if (cell.TryGetValue(out int whole))
        {
            return whole;
        }

        if (cell.TryGetValue(out double number) && number == Math.Truncate(number) && number is >= int.MinValue and <= int.MaxValue)
        {
            return (int)number;
        }

        var text = cell.GetString().Trim();
        if (int.TryParse(text, out whole))
        {
            return whole;
        }

        throw new ManifestFormatException(
            $"{row.Worksheet.Name} row {row.RowNumber()} has a {name} that is not a whole number.");
    }

    private static string ReadRequiredText(IXLRow row, IReadOnlyDictionary<string, int> columns, string name)
    {
        var text = ReadText(row, columns, name);
        if (text.Length == 0)
        {
            throw new ManifestFormatException($"{row.Worksheet.Name} row {row.RowNumber()} is missing {name}.");
        }

        return text;
    }

    private static string ReadText(IXLRow row, IReadOnlyDictionary<string, int> columns, string name)
    {
        var cell = row.Cell(columns[name]);
        return cell.IsEmpty() ? string.Empty : cell.GetString().Trim();
    }

    private static DateTime ReadDate(IXLRow row, IReadOnlyDictionary<string, int> columns)
    {
        var cell = row.Cell(columns["ScheduledDepartureUtc"]);
        if (cell.IsEmpty())
        {
            throw new ManifestFormatException(
                $"{row.Worksheet.Name} row {row.RowNumber()} is missing ScheduledDepartureUtc.");
        }

        if (cell.TryGetValue(out DateTime date))
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        if (cell.TryGetValue(out double serial))
        {
            return DateTime.SpecifyKind(DateTime.FromOADate(serial), DateTimeKind.Utc);
        }

        throw new ManifestFormatException(
            $"{row.Worksheet.Name} row {row.RowNumber()} has a ScheduledDepartureUtc value that is not a date.");
    }
}
