using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AeroLink.Pages;

public class IndexModel : PageModel
{
    // Employee sign-in is a later story. Until that exists, nobody is signed in.
    public bool IsSignedIn => false;

    public IReadOnlyList<ModuleArea> Modules { get; } =
    [
        new()
        {
            Key = "turnaround",
            Name = "Turnaround Planning and Resource Allocation",
            Description = "Manage and plan ground operations turnaround activities efficiently.",
            Link = "/FlightManifest",
        },
        new()
        {
            Key = "baggage",
            Name = "Baggage Unloading, Transfer and Loading Management",
            Description = "Track baggage handling processes and ensure timely transfers between gates.",
            Link = "/BaggageManifest",
        },
        new()
        {
            Key = "departure",
            Name = "Departure Readiness and Turnaround Completion",
            Description = "Prepare for and complete aircraft departures, ensuring smooth transitions.",
            Link = "/DepartureManifest",
        },
    ];

    public string? BlockedModuleName { get; private set; }

    public void OnGet(string? attempt)
    {
        if (IsSignedIn || string.IsNullOrWhiteSpace(attempt))
        {
            return;
        }

        BlockedModuleName = Modules.FirstOrDefault(module => module.Key == attempt)?.Name;
    }

    public sealed class ModuleArea
    {
        public required string Key { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Link { get; init; }
    }
}
