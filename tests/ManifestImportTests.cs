using AeroLink.Data;
using AeroLink.Models;
using AeroLink.Pages;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroLink;

/// <summary>Checks that an uploaded workbook either replaces the manifest or leaves it unchanged.</summary>
/// <remarks>Each case starts from the same saved flight, bag and exception in an in-memory database.</remarks>
public class ManifestImportTests : IDisposable
{
    private readonly BaggageHandlingContext _db;

    /// <summary>Seeds one existing manifest record that a failed import must preserve.</summary>
    public ManifestImportTests()
    {
        var options = new DbContextOptionsBuilder<BaggageHandlingContext>()
            .UseSqlite("Data Source=:memory:").Options;
        _db = new BaggageHandlingContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _db.Flights.Add(new Flight { FlightId = 1, FlightNumber = "OLD1", LoadingStatus = "Started" });
        _db.Bags.Add(new Bag { BagId = 1, FlightId = 1, Tag = "OLD", Outcome = "Pending" });
        _db.Exceptions.Add(new BaggageException
        {
            ExceptionId = 1,
            BagId = 1,
            FlightId = 1,
            Category = "DamagedBag",
            Description = "Cracked shell",
            Status = "Open"
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    /// <summary>Asks for a file when the upload is missing.</summary>
    [Fact]
    public async Task Import_RejectsMissingFileWithoutChangingManifest()
    {
        var page = new BaggageHandlingModel(_db);

        var result = await page.OnPostImportAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Choose an .xlsx workbook to import.", page.Message);
        Assert.False(page.ImportSucceeded);
        await AssertManifestPreservedAsync();
    }

    /// <summary>Asks for a file when the uploaded workbook is empty.</summary>
    [Fact]
    public async Task Import_RejectsEmptyFileWithoutChangingManifest()
    {
        var page = new BaggageHandlingModel(_db)
        {
            Workbook = new UploadFile(new MemoryStream(), "manifest.xlsx")
        };

        await page.OnPostImportAsync();

        Assert.Equal("Choose an .xlsx workbook to import.", page.Message);
        Assert.False(page.ImportSucceeded);
        await AssertManifestPreservedAsync();
    }

    /// <summary>Rejects an upload whose extension is not .xlsx.</summary>
    /// <param name="fileName">Uploaded file name with an unsupported extension.</param>
    [Theory]
    [InlineData("manifest.csv")]
    [InlineData("manifest.xls")]
    public async Task Import_RejectsNonExcelExtension(string fileName)
    {
        var page = new BaggageHandlingModel(_db)
        {
            Workbook = new UploadFile(new MemoryStream([1, 2, 3]), fileName)
        };

        await page.OnPostImportAsync();

        Assert.Equal("The file must be an .xlsx workbook.", page.Message);
        Assert.False(page.ImportSucceeded);
        await AssertManifestPreservedAsync();
    }

    /// <summary>Replaces flights, bags and exceptions for either .xlsx extension casing.</summary>
    /// <param name="fileName">Uploaded workbook name.</param>
    [Theory]
    [InlineData("daily.xlsx")]
    [InlineData("DAILY.XLSX")]
    public async Task Import_ReplacesManifest(string fileName)
    {
        var page = new BaggageHandlingModel(_db)
        {
            Workbook = new UploadFile(ReplacementWorkbook(), fileName)
        };

        var result = await page.OnPostImportAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.ImportSucceeded);
        Assert.Equal("Imported 1 flights, 1 bags, and 1 exceptions.", page.Message);
        Assert.Equal(101, Assert.Single(page.Flights).FlightId);
        Assert.Equal(101, (await _db.Flights.SingleAsync()).FlightId);
        Assert.Equal("Complete", (await _db.Bags.SingleAsync()).Outcome);
        Assert.Equal(6, (await _db.Exceptions.SingleAsync()).ExceptionId);
    }

    /// <summary>Shows the workbook error and keeps the saved manifest when a sheet is missing.</summary>
    [Fact]
    public async Task Import_KeepsManifestWhenWorkbookIsInvalid()
    {
        using var workbook = ReplacementSheets();
        workbook.Worksheets.Delete("Bags");
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var page = new BaggageHandlingModel(_db)
        {
            Workbook = new UploadFile(stream, "broken.xlsx")
        };

        await page.OnPostImportAsync();

        Assert.Equal("The workbook is missing the Bags sheet.", page.Message);
        Assert.False(page.ImportSucceeded);
        await AssertManifestPreservedAsync();
    }

    /// <summary>Shows the generic import error when the file cannot be read.</summary>
    [Fact]
    public async Task Import_KeepsManifestWhenFileCannotBeRead()
    {
        var page = new BaggageHandlingModel(_db)
        {
            Workbook = new UploadFile(new FailingReadStream(), "manifest.xlsx")
        };

        await page.OnPostImportAsync();

        Assert.Equal("The workbook could not be imported. Check that it is a valid .xlsx file.", page.Message);
        Assert.False(page.ImportSucceeded);
        await AssertManifestPreservedAsync();
    }

    /// <summary>Checks that the seeded flight, bag and open exception are still the only records.</summary>
    private async Task AssertManifestPreservedAsync()
    {
        Assert.Equal(1, (await _db.Flights.SingleAsync()).FlightId);
        Assert.Equal("Pending", (await _db.Bags.SingleAsync()).Outcome);
        Assert.Equal("Open", (await _db.Exceptions.SingleAsync()).Status);
    }

    /// <summary>Builds a one-record workbook that should replace the seeded manifest.</summary>
    private static MemoryStream ReplacementWorkbook()
    {
        using var workbook = ReplacementSheets();
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>Creates the three sheets for a replacement manifest.</summary>
    private static XLWorkbook ReplacementSheets()
    {
        var workbook = new XLWorkbook();
        var flights = workbook.AddWorksheet("Flights");
        Write(flights, 1, "FlightId", "FlightLabel", "FlightNumber", "DepartureAirport", "ArrivalAirport", "ScheduledDepartureUtc", "AircraftId", "LoadingStatus");
        Write(flights, 2, 101, "QF101", "DEMO101", "BNE", "SYD", new DateTime(2026, 10, 23, 8, 30, 0), "VH-ABC", "Complete");

        var bags = workbook.AddWorksheet("Bags");
        Write(bags, 1, "BagId", "FlightId", "Tag", "HandlingType", "HandlingInstruction", "Outcome");
        Write(bags, 2, 11, 101, "TAG-11", "Standard", "Keep upright", "Complete");

        var exceptions = workbook.AddWorksheet("Exceptions");
        Write(exceptions, 1, "ExceptionId", "BagId", "FlightId", "Category", "Description", "Status", "DecisionReason");
        Write(exceptions, 2, 6, 11, 101, "MissingBag", "Not in the hold", "Open", "");
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

    /// <summary>Closes the connection and releases this case's database.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Supplies an uploaded file name and stream to the import handler.</summary>
    private sealed class UploadFile : IFormFile
    {
        private readonly Stream _stream;

        /// <summary>Stores the stream and file name the handler inspects.</summary>
        public UploadFile(Stream stream, string fileName)
        {
            _stream = stream;
            FileName = fileName;
            Length = stream.Length;
        }

        public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; }
        public string Name => "Workbook";
        public string FileName { get; }

        /// <summary>Returns the original stream without copying it.</summary>
        public Stream OpenReadStream() => _stream;

        /// <summary>Copies the upload into the destination stream.</summary>
        public void CopyTo(Stream target) => _stream.CopyTo(target);

        /// <summary>Copies the upload into the destination stream asynchronously.</summary>
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            _stream.CopyToAsync(target, cancellationToken);
    }

    /// <summary>Stream whose first read fails, so the import cannot parse the workbook.</summary>
    private sealed class FailingReadStream : Stream
    {
        /// <summary>Reports that the stream can be read.</summary>
        public override bool CanRead => true;

        /// <summary>Reports that the stream can report its length.</summary>
        public override bool CanSeek => true;

        /// <summary>Reports that the stream cannot be written.</summary>
        public override bool CanWrite => false;

        /// <summary>Returns a non-zero length so the upload is not treated as empty.</summary>
        public override long Length => 4;

        /// <summary>Keeps the position at the start because no bytes can be consumed.</summary>
        public override long Position { get => 0; set { } }

        /// <summary>Does nothing because the stream holds no buffered data.</summary>
        public override void Flush() { }

        /// <summary>Fails every read with an IO error.</summary>
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("disk");

        /// <summary>Leaves the position unchanged.</summary>
        public override long Seek(long offset, SeekOrigin origin) => 0;

        /// <summary>Rejects changes to the reported length.</summary>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>Rejects writes because the stream is read-only.</summary>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
