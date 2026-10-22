using AeroLink.Data;
using AeroLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Components;

/// <summary>Renders the flight manifest with an optional flight ID search.</summary>
public class FlightManifestViewComponent : ViewComponent
{
    private readonly BaggageHandlingContext _db;

    /// <summary>Uses the current manifest database to populate the component.</summary>
    public FlightManifestViewComponent(BaggageHandlingContext db)
    {
        _db = db;
    }

    /// <summary>Loads flights in ID order and applies a partial match from the flightId query string.</summary>
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

/// <summary>Search text and matching flights supplied to the manifest table.</summary>
public class FlightManifestSearch
{
    public string FlightId { get; init; } = string.Empty;
    public IReadOnlyList<Flight> Flights { get; init; } = [];
}
