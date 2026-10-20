using AeroLink.Data;
using AeroLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Pages;

public class FlightModel : PageModel
{
    private static readonly Dictionary<string, string> Airports = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BNE"] = "Brisbane",
        ["SYD"] = "Sydney",
        ["MEL"] = "Melbourne",
        ["CBR"] = "Canberra",
        ["CNS"] = "Cairns",
        ["ADL"] = "Adelaide",
        ["PER"] = "Perth",
        ["HBA"] = "Hobart",
        ["DRW"] = "Darwin",
        ["OOL"] = "Gold Coast"
    };

    private readonly BaggageHandlingContext _db;

    public FlightModel(BaggageHandlingContext db)
    {
        _db = db;
    }

    public Flight Flight { get; private set; } = null!;
    public FlightCard Card { get; private set; } = null!;
    public IReadOnlyList<BagRow> Bags { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? BagId { get; set; }

    [BindProperty]
    public string? Outcome { get; set; }

    [BindProperty]
    public string? Acknowledge { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public List<int> BagIds { get; set; } = [];

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

        await LoadAsync(flight);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
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

        var bags = BagIds.Count == 0
            ? []
            : await _db.Bags.Where(bag => bag.FlightId == id && BagIds.Contains(bag.BagId)).ToListAsync();
        var change = new BaggageStatusChange(bags, Outcome, Description);
        if (bags.Count == 0 || !change.IsAllowed
            || (change.RequiresAcknowledgement && Acknowledge != "Yes")
            || (change.NeedsDescription && change.Description.Length == 0))
        {
            await LoadAsync(flight);
            return Page();
        }

        var selectedIds = bags.Select(bag => bag.BagId).ToList();
        var exceptions = await _db.Exceptions.Where(item => selectedIds.Contains(item.BagId)).ToListAsync();
        await change.ApplyAsync(_db, exceptions);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, bagId = BagId });
    }

    private async Task LoadAsync(Flight flight)
    {
        Flight = flight;
        var bags = await _db.Bags.Where(bag => bag.FlightId == flight.FlightId).OrderBy(bag => bag.BagId).ToListAsync();
        var bagIds = bags.Select(bag => bag.BagId).ToList();
        var exceptions = await _db.Exceptions.Where(item => bagIds.Contains(item.BagId)).ToListAsync();
        var byBag = exceptions.GroupBy(item => item.BagId).ToDictionary(group => group.Key, group => group.First());

        var rows = bags.Select(bag => new BagRow(bag, byBag.GetValueOrDefault(bag.BagId))).ToList();
        Card = new FlightCard(flight, AirportName(flight.DepartureAirport), AirportName(flight.ArrivalAirport), rows);
        var search = BagId?.Trim() ?? string.Empty;
        Bags = search.Length == 0
            ? rows
            : rows.Where(row => row.Bag.BagId.ToString().Contains(search, StringComparison.Ordinal)).ToList();
    }

    private static string AirportName(string code) =>
        Airports.TryGetValue(code, out var name) ? name : code;
}

public sealed class FlightCard
{
    public FlightCard(Flight flight, string originName, string destinationName, IReadOnlyList<BagRow> bags)
    {
        Flight = flight;
        OriginName = originName;
        DestinationName = destinationName;
        Loaded = bags.Count(bag => bag.Status == "Complete");
        Total = bags.Count;
    }

    public Flight Flight { get; }
    public string OriginName { get; }
    public string DestinationName { get; }
    public int Loaded { get; }
    public int Total { get; }
}

public sealed class BagRow
{
    public BagRow(Bag bag, BaggageException? exception)
    {
        Bag = bag;
        Status = exception?.Category switch
        {
            "MissingBag" => "Missing",
            "DamagedBag" => "Damaged",
            _ => bag.Outcome
        };
    }

    public Bag Bag { get; }
    public string Status { get; }
    public bool IsSpecial => !string.Equals(Bag.HandlingType, "Standard", StringComparison.OrdinalIgnoreCase);
}

public sealed class BaggageStatusChange
{
    private static readonly Dictionary<string, string> Categories = new()
    {
        ["Missing"] = "MissingBag",
        ["Damaged"] = "DamagedBag"
    };

    public BaggageStatusChange(IReadOnlyList<Bag> bags, string? outcome, string? description)
    {
        Bags = bags;
        Outcome = outcome ?? string.Empty;
        Description = description?.Trim() ?? string.Empty;
    }

    public IReadOnlyList<Bag> Bags { get; }
    public string Outcome { get; }
    public string Description { get; }
    public bool IsAllowed => Outcome is "Pending" or "Complete" or "Missing" or "Damaged";
    public bool NeedsDescription => Outcome is "Missing" or "Damaged";
    public bool RequiresAcknowledgement => Bags.Any(bag => !string.Equals(bag.HandlingType, "Standard", StringComparison.OrdinalIgnoreCase));

    public async Task ApplyAsync(BaggageHandlingContext db, IReadOnlyList<BaggageException> exceptions)
    {
        var nextId = await db.Exceptions.MaxAsync(item => (int?)item.ExceptionId) ?? 0;
        var byBag = exceptions.GroupBy(item => item.BagId).ToDictionary(group => group.Key, group => group.ToList());

        foreach (var bag in Bags)
        {
            bag.Outcome = Outcome;
            if (!byBag.TryGetValue(bag.BagId, out var existing))
            {
                existing = [];
            }

            if (Categories.TryGetValue(Outcome, out var category))
            {
                if (existing.Count == 0)
                {
                    db.Exceptions.Add(new BaggageException
                    {
                        ExceptionId = ++nextId,
                        BagId = bag.BagId,
                        FlightId = bag.FlightId,
                        Category = category,
                        Description = Description,
                        Status = "Open",
                        DecisionReason = string.Empty
                    });
                }
                else
                {
                    existing[0].Category = category;
                    existing[0].Description = Description;
                    db.Exceptions.RemoveRange(existing.Skip(1));
                }
            }
            else
            {
                db.Exceptions.RemoveRange(existing);
            }
        }
    }
}
