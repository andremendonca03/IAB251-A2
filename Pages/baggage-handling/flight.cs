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
    public bool IsSupervisor => User.IsInRole("BG_Supervisor");
    public bool CanUpdateBags => IsSupervisor || User.IsInRole("Baggage Handler");

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

        if (!CanUpdateBags)
        {
            return Forbid();
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
        if (Outcome == "Complete" && exceptions.Count > 0)
        {
            ModelState.AddModelError(string.Empty, "Bags with an exception must be approved using the supervisor's Load or Keep unloaded buttons.");
            await LoadAsync(flight);
            return Page();
        }

        await change.ApplyAsync(_db, exceptions);
        await UpdateLoadingStatusAsync(flight);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, bagId = BagId });
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, int approvalBagId, string decision, string? justification)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        if (!IsSupervisor)
        {
            return Forbid();
        }

        if (decision is not ("Load" or "KeepUnloaded"))
        {
            return BadRequest();
        }

        var flight = await _db.Flights.FirstOrDefaultAsync(item => item.FlightId == id);
        var bag = await _db.Bags.FirstOrDefaultAsync(item => item.FlightId == id && item.BagId == approvalBagId);
        if (flight is null || bag is null)
        {
            return NotFound();
        }

        var exceptions = await _db.Exceptions.Where(item => item.FlightId == id && item.BagId == approvalBagId).ToListAsync();
        if (exceptions.Count == 0 || exceptions.All(item => BagRow.IsApproved(item)))
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(justification))
        {
            ModelState.AddModelError(string.Empty, "Provide a justification before approving this bag.");
            await LoadAsync(flight);
            return Page();
        }

        if (!string.Equals(bag.HandlingType, "Standard", StringComparison.OrdinalIgnoreCase) && Acknowledge != "Yes")
        {
            ModelState.AddModelError(string.Empty, "Acknowledge the handling instructions before approving this bag.");
            await LoadAsync(flight);
            return Page();
        }

        bag.Outcome = decision == "Load" ? "Complete" : "Unloaded";
        foreach (var exception in exceptions)
        {
            exception.Status = decision == "Load" ? "ApprovedToLoad" : "KeptUnloaded";
            exception.DecisionReason = $"{(decision == "Load" ? "Load" : "Keep unloaded")} approved by {User.Identity!.Name}: {justification.Trim()}";
        }

        await UpdateLoadingStatusAsync(flight);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, bagId = BagId });
    }

    private async Task UpdateLoadingStatusAsync(Flight flight)
    {
        var bags = await _db.Bags.Where(bag => bag.FlightId == flight.FlightId).ToListAsync();
        var exceptions = await _db.Exceptions.Where(item => item.FlightId == flight.FlightId).ToListAsync();
        flight.LoadingStatus = bags.Count > 0
            && bags.All(bag => bag.Outcome == "Complete"
                || (bag.Outcome == "Unloaded" && exceptions.Any(item => item.BagId == bag.BagId && item.Status == "KeptUnloaded")))
            && exceptions.All(item => BagRow.IsApproved(item)) ? "Complete" : "Started";
    }

    private async Task LoadAsync(Flight flight)
    {
        Flight = flight;
        var bags = await _db.Bags.Where(bag => bag.FlightId == flight.FlightId).OrderBy(bag => bag.BagId).ToListAsync();
        var bagIds = bags.Select(bag => bag.BagId).ToList();
        var exceptions = await _db.Exceptions.Where(item => bagIds.Contains(item.BagId)).ToListAsync();
        var byBag = exceptions.GroupBy(item => item.BagId).ToDictionary(group => group.Key, group => group.ToList());

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
        Loaded = bags.Count(bag => bag.Status == "Complete" && !bag.NeedsApproval);
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
    public BagRow(Bag bag, IReadOnlyList<BaggageException>? exceptions)
    {
        Bag = bag;
        exceptions ??= [];
        var pending = exceptions.FirstOrDefault(item => !IsApproved(item));
        NeedsApproval = pending is not null;
        HasException = exceptions.Count > 0;
        Reason = string.Join("; ", exceptions.Select(item => item.Description));
        Approval = NeedsApproval ? "Awaiting approval" : exceptions.FirstOrDefault()?.DecisionReason ?? string.Empty;
        Status = pending?.Category switch
        {
            "MissingBag" => "Missing",
            "DamagedBag" => "Damaged",
            _ => bag.Outcome
        };
    }

    public Bag Bag { get; }
    public string Status { get; }
    public bool NeedsApproval { get; }
    public bool HasException { get; }
    public string Reason { get; }
    public string Approval { get; }
    public static bool IsApproved(BaggageException exception) => exception.Status is "ApprovedToLoad" or "KeptUnloaded";
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

            foreach (var exception in existing)
            {
                exception.Status = "Open";
                exception.DecisionReason = string.Empty;
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
                }
            }
        }
    }
}
