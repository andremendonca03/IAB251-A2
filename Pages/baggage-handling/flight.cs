using AeroLink.Data;
using AeroLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Pages;

public class FlightModel : PageModel
{
    private readonly BaggageHandlingContext _db;

    public FlightModel(BaggageHandlingContext db)
    {
        _db = db;
    }

    public Flight Flight { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        var flight = await _db.Flights.FirstOrDefaultAsync(flight => flight.FlightId == id);
        if (flight is null)
        {
            return NotFound();
        }

        Flight = flight;
        return Page();
    }
}