using AeroLink.Data;
using AeroLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Pages;

/// <summary>Builds an on-screen loading report for supervisors from the current manifest.</summary>
public class SummaryReportModel : PageModel
{
    private readonly BaggageHandlingContext _db;

    /// <summary>Uses the manifest database as the report source.</summary>
    public SummaryReportModel(BaggageHandlingContext db)
    {
        _db = db;
    }

    public List<FlightSummary> Reports { get; } = [];
    public DateTime GeneratedAtUtc { get; private set; }

    /// <summary>Includes completed flights only when all bags are resolved and exceptions are approved.</summary>
    /// <remarks>The report is regenerated on each request; it is not a stored historical snapshot.</remarks>
    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        if (!User.IsInRole("BG_Supervisor"))
        {
            return Forbid();
        }

        var flights = await _db.Flights.AsNoTracking()
            .Where(flight => flight.LoadingStatus == "Complete")
            .OrderBy(flight => flight.ScheduledDepartureUtc).ThenBy(flight => flight.FlightId).ToListAsync();
        var flightIds = flights.Select(flight => flight.FlightId).ToList();
        var bags = await _db.Bags.AsNoTracking().Where(bag => flightIds.Contains(bag.FlightId)).ToListAsync();
        var exceptions = await _db.Exceptions.AsNoTracking()
            .Where(item => flightIds.Contains(item.FlightId)).OrderBy(item => item.BagId).ToListAsync();

        foreach (var flight in flights)
        {
            var flightBags = bags.Where(bag => bag.FlightId == flight.FlightId).ToList();
            var flightExceptions = exceptions.Where(item => item.FlightId == flight.FlightId).ToList();

            // Imported manifests must also have resolved bags and approved exceptions.
            if (flightBags.Count == 0 || flightExceptions.Any(item => !BagRow.IsApproved(item))
                || flightBags.Any(bag => bag.Outcome != "Complete"
                    && !(bag.Outcome == "Unloaded" && flightExceptions.Any(item => item.BagId == bag.BagId && item.Status == "KeptUnloaded"))))
            {
                continue;
            }

            Reports.Add(new FlightSummary
            {
                Flight = flight,
                Total = flightBags.Count,
                Loaded = flightBags.Count(bag => bag.Outcome == "Complete"),
                Unloaded = flightBags.Count(bag => bag.Outcome == "Unloaded"),
                Exceptions = flightExceptions
            });
        }

        GeneratedAtUtc = DateTime.UtcNow;
        return Page();
    }

    /// <summary>Bag totals and exception decisions for one finalised flight.</summary>
    public sealed class FlightSummary
    {
        public required Flight Flight { get; init; }
        public int Total { get; init; }
        public int Loaded { get; init; }
        public int Unloaded { get; init; }
        public required IReadOnlyList<BaggageException> Exceptions { get; init; }
        /// <summary>Number of distinct bags with exceptions, regardless of how many issues each has.</summary>
        public int ExceptionBags => Exceptions.Select(item => item.BagId).Distinct().Count();
    }
}
