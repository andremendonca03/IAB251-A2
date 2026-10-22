using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AeroLink.Pages;

/// <summary>Supplies the home page's module cards and blocked-access messages.</summary>
public class IndexModel : PageModel
{
    /// <summary>Legacy flag used by the blocked-message handler; the view checks the authenticated user directly.</summary>
    public bool IsSignedIn => false;

    public IReadOnlyList<ModuleArea> Modules { get; } =
    [
        new()
        {
            Key = "turnaround",
            Name = "Turnaround Planning and Resource Allocation",
            Description = "Manage and plan ground operations turnaround activities efficiently.",
            Link = "",
        },
        new()
        {
            Key = "baggage",
            Name = "Baggage Unloading, Transfer and Loading Management",
            Description = "Track baggage handling processes and ensure timely transfers between gates.",
            Link = "/baggage-handling",
        },
        new()
        {
            Key = "departure",
            Name = "Departure Readiness and Turnaround Completion",
            Description = "Prepare for and complete aircraft departures, ensuring smooth transitions.",
            Link = "",
        },
    ];

    public string? BlockedModuleName { get; private set; }
    public string? BlockedModuleCause { get; private set; }

    /// <summary>Identifies the attempted module and the reason supplied for blocking access.</summary>
    public void OnGet(string? attempt, string? cause)
    {
        if (IsSignedIn || string.IsNullOrWhiteSpace(attempt))
        {
            return;
        }

        BlockedModuleName = Modules.FirstOrDefault(module => module.Key == attempt)?.Name;
        BlockedModuleCause = cause;
    }

    /// <summary>Display text and destination for a home-page module card.</summary>
    public sealed class ModuleArea
    {
        public required string Key { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Link { get; init; }
    }
}
