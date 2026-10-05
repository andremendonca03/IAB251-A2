using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AeroLink.Pages;

public class IndexModel : PageModel
{
    // Employee sign-in is a later story. Until that exists, nobody is signed in.
    public bool IsSignedIn => false;

    public IReadOnlyList<ModuleArea> Modules { get; } =
    [
        new("turnaround", "Turnaround Planning and Resource Allocation"),
        new("baggage", "Baggage Unloading, Transfer and Loading Management"),
        new("departure", "Departure Readiness and Turnaround Completion"),
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

    public sealed record ModuleArea(string Key, string Name);
}
