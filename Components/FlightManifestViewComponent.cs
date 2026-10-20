using AeroLink.Data;
using AeroLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Components;

public class FlightManifestViewComponent : ViewComponent
{
    private readonly BaggageHandlingContext _db;

    public FlightManifestViewComponent(BaggageHandlingContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var flightId = Request.Query["flightId"].ToString().Trim();
        var flights = await _db.Flights.OrderBy(flight => flight.FlightId).ToListAsync();

        if (flightId.Length > 0)
        {
            flights = flights
                .Where(flight => flight.FlightId.ToString().Contains(flightId, StringComparison.Ordinal))
                .ToList();
        }

        return View(new FlightManifestSearch
        {
            FlightId = flightId,
            Flights = flights
        });
    }
}

public class FlightManifestSearch
{
    public string FlightId { get; init; } = string.Empty;
    public IReadOnlyList<Flight> Flights { get; init; } = [];
}
