using AeroLink.Data;
using AeroLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Pages;

public class BaggageHandlingModel : PageModel
{
    private readonly BaggageHandlingContext _db;

    public BaggageHandlingModel(BaggageHandlingContext db)
    {
        _db = db;
    }

    [BindProperty]
    public IFormFile? Workbook { get; set; }

    public IReadOnlyList<Flight> Flights { get; private set; } = [];
    public IReadOnlyList<Bag> Bags { get; private set; } = [];
    public IReadOnlyList<BaggageException> Exceptions { get; private set; } = [];
    public string? Message { get; private set; }
    public bool ImportSucceeded { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (Workbook is null || Workbook.Length == 0)
        {
            Message = "Choose an .xlsx workbook to import.";
            await LoadAsync();
            return Page();
        }

        if (!string.Equals(Path.GetExtension(Workbook.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            Message = "The file must be an .xlsx workbook.";
            await LoadAsync();
            return Page();
        }

        try
        {
            await using var stream = Workbook.OpenReadStream();
            var import = ManifestWorkbook.Read(stream);

            await using var transaction = await _db.Database.BeginTransactionAsync();
            await _db.Exceptions.ExecuteDeleteAsync();
            await _db.Bags.ExecuteDeleteAsync();
            await _db.Flights.ExecuteDeleteAsync();
            _db.Flights.AddRange(import.Flights);
            _db.Bags.AddRange(import.Bags);
            _db.Exceptions.AddRange(import.Exceptions);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            ImportSucceeded = true;
            Message = $"Imported {import.Flights.Count} flights, {import.Bags.Count} bags, and {import.Exceptions.Count} exceptions.";
        }
        catch (ManifestFormatException ex)
        {
            Message = ex.Message;
        }
        catch (Exception)
        {
            Message = "The workbook could not be imported. Check that it is a valid .xlsx file.";
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Flights = await _db.Flights.OrderBy(flight => flight.FlightId).ToListAsync();
        Bags = await _db.Bags.OrderBy(bag => bag.BagId).ToListAsync();
        Exceptions = await _db.Exceptions.OrderBy(item => item.ExceptionId).ToListAsync();
    }
}
