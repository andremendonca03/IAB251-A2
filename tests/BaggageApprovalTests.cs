using System.Security.Claims;
using AeroLink.Data;
using AeroLink.Models;
using AeroLink.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroLink;

/// <summary>Checks justification, access rules and saved decisions in the baggage approval handlers.</summary>
/// <remarks>Each case uses an isolated SQLite database and calls handlers directly, without HTTP middleware or a browser.</remarks>
public class BaggageApprovalTests : IDisposable
{
    private readonly BaggageHandlingContext _db;

    /// <summary>Seeds one damaged bag with an open exception in a new in-memory database.</summary>
    public BaggageApprovalTests()
    {
        var options = new DbContextOptionsBuilder<BaggageHandlingContext>()
            .UseSqlite("Data Source=:memory:").Options;
        _db = new BaggageHandlingContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _db.Flights.Add(new Flight { FlightId = 1, LoadingStatus = "Started" });
        _db.Bags.Add(new Bag { BagId = 1, FlightId = 1, HandlingType = "Standard", Outcome = "Damaged" });
        _db.Exceptions.Add(new BaggageException
        {
            ExceptionId = 1,
            BagId = 1,
            FlightId = 1,
            Category = "DamagedBag",
            Description = "Broken handle",
            Status = "Open"
        });
        _db.SaveChanges();
    }

    /// <summary>Rejects absent or whitespace-only justifications without changing the manifest.</summary>
    [Theory]
    [InlineData("Load", null)]
    [InlineData("Load", "")]
    [InlineData("Load", " \t\r\n ")]
    [InlineData("KeepUnloaded", null)]
    [InlineData("KeepUnloaded", "")]
    [InlineData("KeepUnloaded", " \t\r\n ")]
    public async Task Approval_RejectsMissingOrBlankJustification(string decision, string? justification)
    {
        var page = CreatePage();

        var result = await page.OnPostApproveAsync(1, 1, decision, justification);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Contains(page.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Provide a justification before approving this bag.");
        await AssertUnchangedAsync();
    }

    /// <summary>Persists either decision with a trimmed justification, preserving the issue and updating totals.</summary>
    [Theory]
    [InlineData("Load", "Standard", "Complete", "ApprovedToLoad", "Load")]
    [InlineData("KeepUnloaded", "Standard", "Unloaded", "KeptUnloaded", "Keep unloaded")]
    [InlineData("Load", "Fragile", "Complete", "ApprovedToLoad", "Load")]
    [InlineData("KeepUnloaded", "Fragile", "Unloaded", "KeptUnloaded", "Keep unloaded")]
    public async Task Approval_SavesDecisionAndTrimmedJustification(
        string decision, string handlingType, string outcome, string status, string decisionLabel)
    {
        (await _db.Bags.SingleAsync()).HandlingType = handlingType;
        await _db.SaveChangesAsync();
        var page = CreatePage();
        page.Acknowledge = handlingType == "Standard" ? null : "Yes";

        var result = await page.OnPostApproveAsync(1, 1, decision, "  Inspected the damaged handle.  ");

        Assert.IsType<RedirectToPageResult>(result);
        // Read fresh objects to verify that approval was actually saved.
        _db.ChangeTracker.Clear();
        var bag = await _db.Bags.SingleAsync();
        var exception = await _db.Exceptions.SingleAsync();
        Assert.Equal(outcome, bag.Outcome);
        Assert.Equal(status, exception.Status);
        Assert.Equal($"{decisionLabel} approved by supervisor@example.com: Inspected the damaged handle.", exception.DecisionReason);
        Assert.Equal("Broken handle", exception.Description);
        Assert.Equal("Complete", (await _db.Flights.SingleAsync()).LoadingStatus);

        await page.OnGetAsync(1);
        Assert.False(Assert.Single(page.Bags).NeedsApproval);
        Assert.Equal(exception.DecisionReason, page.Bags[0].Approval);
        Assert.Equal(decision == "Load" ? 1 : 0, page.Card.Loaded);
    }

    /// <summary>Prevents handlers and unrelated roles from approving either loading decision.</summary>
    [Theory]
    [InlineData("Baggage Handler", "Load")]
    [InlineData("Baggage Handler", "KeepUnloaded")]
    [InlineData("Pilot", "Load")]
    [InlineData("Pilot", "KeepUnloaded")]
    public async Task Approval_RejectsUsersWhoAreNotSupervisors(string role, string decision)
    {
        var page = CreatePage(role);

        var result = await page.OnPostApproveAsync(1, 1, decision, "Inspected the bag.");

        Assert.IsType<ForbidResult>(result);
        await AssertUnchangedAsync();
    }

    /// <summary>Redirects unauthenticated approval requests to login without changing data.</summary>
    [Fact]
    public async Task Approval_RedirectsAnonymousUserToLogin()
    {
        var page = CreatePage(null);

        var result = await page.OnPostApproveAsync(1, 1, "Load", "Inspected the bag.");

        Assert.Equal("/Login", Assert.IsType<RedirectToPageResult>(result).PageName);
        await AssertUnchangedAsync();
    }

    /// <summary>Requires handling acknowledgement for fragile bags even when justification is valid.</summary>
    [Theory]
    [InlineData("Load")]
    [InlineData("KeepUnloaded")]
    public async Task Approval_RequiresAcknowledgementForSpecialBaggage(string decision)
    {
        (await _db.Bags.SingleAsync()).HandlingType = "Fragile";
        await _db.SaveChangesAsync();
        var page = CreatePage();

        var result = await page.OnPostApproveAsync(1, 1, decision, "Inspected the bag.");

        Assert.IsType<PageResult>(result);
        Assert.Contains(page.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Acknowledge the handling instructions before approving this bag.");
        await AssertUnchangedAsync();
    }

    /// <summary>Rejects missing records and approval requests using a different flight's bag.</summary>
    [Theory]
    [InlineData(99, 1)]
    [InlineData(1, 99)]
    [InlineData(2, 1)]
    public async Task Approval_RejectsMissingBagOrWrongFlight(int flightId, int bagId)
    {
        _db.Flights.Add(new Flight { FlightId = 2, LoadingStatus = "NotStarted" });
        await _db.SaveChangesAsync();
        var page = CreatePage();

        var result = await page.OnPostApproveAsync(flightId, bagId, "Load", "Inspected the bag.");

        Assert.IsType<NotFoundResult>(result);
        await AssertUnchangedAsync();
        Assert.Equal("NotStarted", (await _db.Flights.AsNoTracking().SingleAsync(f => f.FlightId == 2)).LoadingStatus);
    }

    /// <summary>Rejects decision values outside Load and KeepUnloaded.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("Complete")]
    [InlineData("Invalid")]
    public async Task Approval_RejectsInvalidDecision(string decision)
    {
        var result = await CreatePage().OnPostApproveAsync(1, 1, decision, "Inspected the bag.");

        Assert.IsType<BadRequestResult>(result);
        await AssertUnchangedAsync();
    }

    /// <summary>Prevents the exception approval endpoint from updating an ordinary bag.</summary>
    [Fact]
    public async Task Approval_RejectsBagWithoutAnException()
    {
        _db.Bags.Add(new Bag { BagId = 2, FlightId = 1, HandlingType = "Standard", Outcome = "Pending" });
        await _db.SaveChangesAsync();

        var result = await CreatePage().OnPostApproveAsync(1, 2, "Load", "Inspected the bag.");

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Pending", (await _db.Bags.AsNoTracking().SingleAsync(b => b.BagId == 2)).Outcome);
        await AssertUnchangedAsync();
    }

    /// <summary>Preserves an existing sign-off when another decision is submitted without reopening the issue.</summary>
    [Theory]
    [InlineData("Load", "KeepUnloaded", "Complete", "ApprovedToLoad")]
    [InlineData("KeepUnloaded", "Load", "Unloaded", "KeptUnloaded")]
    public async Task Approval_CannotOverwriteAnExistingSignOff(
        string firstDecision, string nextDecision, string outcome, string status)
    {
        await CreatePage().OnPostApproveAsync(1, 1, firstDecision, "Original justification.");

        var result = await CreatePage().OnPostApproveAsync(1, 1, nextDecision, "Replacement justification.");

        Assert.IsType<BadRequestResult>(result);
        var exception = await _db.Exceptions.AsNoTracking().SingleAsync();
        Assert.Equal(status, exception.Status);
        Assert.EndsWith(": Original justification.", exception.DecisionReason);
        Assert.Equal(outcome, (await _db.Bags.AsNoTracking().SingleAsync()).Outcome);
    }

    /// <summary>Keeps the flight in progress when approving one bag leaves another unresolved.</summary>
    [Theory]
    [InlineData("Load")]
    [InlineData("KeepUnloaded")]
    public async Task Approval_KeepsFlightStartedWhileOtherBagsArePending(string decision)
    {
        _db.Bags.Add(new Bag { BagId = 2, FlightId = 1, HandlingType = "Standard", Outcome = "Pending" });
        await _db.SaveChangesAsync();

        var result = await CreatePage().OnPostApproveAsync(1, 1, decision, "Inspected the bag.");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Started", (await _db.Flights.AsNoTracking().SingleAsync()).LoadingStatus);
        Assert.Equal("Pending", (await _db.Bags.AsNoTracking().SingleAsync(b => b.BagId == 2)).Outcome);
    }

    /// <summary>Prevents both baggage roles from bypassing exception approval through the ordinary status form.</summary>
    [Theory]
    [InlineData("Baggage Handler")]
    [InlineData("BG_Supervisor")]
    public async Task StatusUpdate_CannotBypassExceptionApproval(string role)
    {
        var page = CreatePage(role);
        page.BagIds = [1];
        page.Outcome = "Complete";

        var result = await page.OnPostAsync(1);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        await AssertUnchangedAsync();
    }

    /// <summary>Requires a fresh justification after a status change reopens an approved issue.</summary>
    [Theory]
    [InlineData("Load")]
    [InlineData("KeepUnloaded")]
    public async Task StatusUpdate_ReopensApprovalAndRequiresNewJustification(string decision)
    {
        await CreatePage().OnPostApproveAsync(1, 1, decision, "Original justification.");
        var page = CreatePage("Baggage Handler");
        page.BagIds = [1];
        page.Outcome = "Pending";

        var result = await page.OnPostAsync(1);

        Assert.IsType<RedirectToPageResult>(result);
        var exception = await _db.Exceptions.AsNoTracking().SingleAsync();
        Assert.Equal("Open", exception.Status);
        Assert.Equal(string.Empty, exception.DecisionReason);
        Assert.Equal("Broken handle", exception.Description);
        Assert.Equal("Started", (await _db.Flights.AsNoTracking().SingleAsync()).LoadingStatus);
        Assert.IsType<PageResult>(await CreatePage().OnPostApproveAsync(1, 1, decision, null));
        Assert.Equal("Open", (await _db.Exceptions.AsNoTracking().SingleAsync()).Status);
    }

    /// <summary>Creates a page handler with test identity claims and the test database.</summary>
    /// <param name="role">Role to authenticate; null creates an anonymous user.</param>
    private FlightModel CreatePage(string? role = "BG_Supervisor")
    {
        var identity = role is null ? new ClaimsIdentity() : new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.Name, "supervisor@example.com")], "Test");
        return new FlightModel(_db)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    /// <summary>Checks that rejected requests leave the seeded bag, exception and flight untouched.</summary>
    private async Task AssertUnchangedAsync()
    {
        Assert.False(_db.ChangeTracker.HasChanges());
        var exception = await _db.Exceptions.AsNoTracking().SingleAsync();
        Assert.Equal("Open", exception.Status);
        Assert.Equal(string.Empty, exception.DecisionReason);
        Assert.Equal("Broken handle", exception.Description);
        Assert.Equal("Damaged", (await _db.Bags.AsNoTracking().SingleAsync(b => b.BagId == 1)).Outcome);
        Assert.Equal("Started", (await _db.Flights.AsNoTracking().SingleAsync(f => f.FlightId == 1)).LoadingStatus);
    }

    /// <summary>Closes the connection and releases the test's in-memory database.</summary>
    public void Dispose() => _db.Dispose();
}
