using AeroLink.Data;
using ClosedXML.Excel;
using Xunit;

namespace AeroLink;

/// <summary>Checks that a manifest workbook is read and rejected before it reaches the database.</summary>
public class ManifestWorkbookTests
{
    /// <summary>Reads trimmed text, a text bag id, a UTC departure and a blank decision.</summary>
    [Fact]
    public void Read_ParsesValidSheetsAndSkipsBlankRows()
    {
        using var stream = Workbook();

        var import = ManifestWorkbook.Read(stream);

        Assert.Equal([101, 102], import.Flights.Select(flight => flight.FlightId).ToArray());
        Assert.Equal("QF101", import.Flights[0].FlightLabel);
        Assert.Equal(new DateTime(2026, 10, 23, 8, 30, 0, DateTimeKind.Utc), import.Flights[0].ScheduledDepartureUtc);
        var bag = Assert.Single(import.Bags);
        Assert.Equal(11, bag.BagId);
        Assert.Equal("TAG-11", bag.Tag);
        var exception = Assert.Single(import.Exceptions);
        Assert.Equal("Not in the hold", exception.Description);
        Assert.Equal(string.Empty, exception.DecisionReason);
    }

    /// <summary>Rejects a workbook that does not contain the Bags sheet.</summary>
    [Fact]
    public void Read_RejectsMissingSheet()
    {
        using var stream = Workbook(workbook => workbook.Worksheets.Delete("Bags"));

        var error = Assert.Throws<ManifestFormatException>(() => ManifestWorkbook.Read(stream));

        Assert.Equal("The workbook is missing the Bags sheet.", error.Message);
    }

    /// <summary>Rejects a Flights sheet that is missing a required column.</summary>
    [Fact]
    public void Read_RejectsMissingColumn()
    {
        using var stream = Workbook(workbook => workbook.Worksheet("Flights").Cell(1, 7).Clear());

        var error = Assert.Throws<ManifestFormatException>(() => ManifestWorkbook.Read(stream));

        Assert.Equal("Flights is missing columns: AircraftId.", error.Message);
    }

    /// <summary>Rejects a second row that reuses a flight, bag or exception id.</summary>
    /// <param name="sheet">Sheet that contains the repeated id.</param>
    /// <param name="row">Destination row that copies the first data row.</param>
    /// <param name="columns">Number of columns that must be copied for the row to be valid.</param>
    /// <param name="message">Expected validation message.</param>
    [Theory]
    [InlineData("Flights", 4, 8, "Flights has a duplicate FlightId 101.")]
    [InlineData("Bags", 3, 6, "Bags has a duplicate BagId 11.")]
    [InlineData("Exceptions", 3, 7, "Exceptions has a duplicate ExceptionId 6.")]
    public void Read_RejectsDuplicateIds(string sheet, int row, int columns, string message)
    {
        using var stream = Workbook(workbook =>
        {
            var cells = workbook.Worksheet(sheet);
            for (var column = 1; column <= columns; column++)
            {
                cells.Cell(row, column).Value = cells.Cell(2, column).Value;
            }
        });

        var error = Assert.Throws<ManifestFormatException>(() => ManifestWorkbook.Read(stream));

        Assert.Equal(message, error.Message);
    }

    /// <summary>Rejects a required flight label that is only spaces.</summary>
    [Fact]
    public void Read_RejectsBlankRequiredText()
    {
        using var stream = Workbook(workbook => workbook.Worksheet("Flights").Cell(2, 2).Value = "   ");

        var error = Assert.Throws<ManifestFormatException>(() => ManifestWorkbook.Read(stream));

        Assert.Equal("Flights row 2 is missing FlightLabel.", error.Message);
    }

    /// <summary>Rejects a flight id that is not a whole number.</summary>
    [Fact]
    public void Read_RejectsFractionalId()
    {
        using var stream = Workbook(workbook => workbook.Worksheet("Flights").Cell(2, 1).Value = 10.5);

        var error = Assert.Throws<ManifestFormatException>(() => ManifestWorkbook.Read(stream));

        Assert.Equal("Flights row 2 has a FlightId that is not a whole number.", error.Message);
    }

    /// <summary>Rejects a departure value that is not a date.</summary>
    [Fact]
    public void Read_RejectsInvalidDeparture()
    {
        using var stream = Workbook(workbook => workbook.Worksheet("Flights").Cell(2, 6).Value = "tomorrow");

        var error = Assert.Throws<ManifestFormatException>(() => ManifestWorkbook.Read(stream));

        Assert.Equal("Flights row 2 has a ScheduledDepartureUtc value that is not a date.", error.Message);
    }

    /// <summary>Rejects bytes that are not an Excel workbook.</summary>
    [Fact]
    public void Read_RejectsFileThatIsNotAWorkbook()
    {
        using var stream = new MemoryStream("not a workbook"u8.ToArray());

        var error = Assert.Throws<ManifestFormatException>(() => ManifestWorkbook.Read(stream));

        Assert.Equal("The file is not a valid .xlsx workbook.", error.Message);
    }

    /// <summary>Builds the sample workbook and applies one change before saving it.</summary>
    /// <param name="change">Optional edit made after the valid sheets are created.</param>
    private static MemoryStream Workbook(Action<XLWorkbook>? change = null)
    {
        using var workbook = SampleWorkbook();
        change?.Invoke(workbook);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>Creates two flights, one bag and one exception, with a blank row between the flights.</summary>
    private static XLWorkbook SampleWorkbook()
    {
        var workbook = new XLWorkbook();
        var flights = workbook.AddWorksheet("Flights");
        Write(flights, 1, "FlightId", "FlightLabel", "FlightNumber", "DepartureAirport", "ArrivalAirport", "ScheduledDepartureUtc", "AircraftId", "LoadingStatus");
        Write(flights, 2, 101, "  QF101  ", "DEMO101", "BNE", "SYD", new DateTime(2026, 10, 23, 8, 30, 0), "VH-ABC", "Complete");
        Write(flights, 4, 102, "QF102", "DEMO102", "SYD", "MEL", new DateTime(2026, 10, 23, 9, 0, 0), "VH-DEF", "NotStarted");

        var bags = workbook.AddWorksheet("Bags");
        Write(bags, 1, "BagId", "FlightId", "Tag", "HandlingType", "HandlingInstruction", "Outcome");
        Write(bags, 2, "11", 101, "  TAG-11  ", "Standard", "Keep upright", "Complete");

        var exceptions = workbook.AddWorksheet("Exceptions");
        Write(exceptions, 1, "ExceptionId", "BagId", "FlightId", "Category", "Description", "Status", "DecisionReason");
        Write(exceptions, 2, 6, 11, 101, "MissingBag", "  Not in the hold  ", "Open", "");
        return workbook;
    }

    /// <summary>Writes one worksheet row from left to right.</summary>
    private static void Write(IXLWorksheet sheet, int row, params object[] values)
    {
        for (var column = 0; column < values.Length; column++)
        {
            sheet.Cell(row, column + 1).Value = XLCellValue.FromObject(values[column]);
        }
    }
}
