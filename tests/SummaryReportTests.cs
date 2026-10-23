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

/// <summary>Checks which completed flights a supervisor's loading report includes.</summary>
/// <remarks>Each case uses its own in-memory SQLite database and calls the page handler directly.</remarks>
public class SummaryReportTests : IDisposable
{
    private readonly BaggageHandlingContext _db;

    /// <summary>Opens a new empty manifest database.</summary>
    public SummaryReportTests()
    {
        var options = new DbContextOptionsBuilder<BaggageHandlingContext>()
            .UseSqlite("Data Source=:memory:").Options;
        _db = new BaggageHandlingContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    /// <summary>Sends a signed-out visitor to the login page.</summary>
    [Fact]
    public async Task Report_RedirectsAnonymousUserToLogin()
    {
        var result = await CreatePage(null).OnGetAsync();

        Assert.Equal("/Login", Assert.IsType<RedirectToPageResult>(result).PageName);
    }

    /// <summary>Hides the report from every role other than the baggage supervisor.</summary>
    /// <param name="role">Authenticated role that must not receive the report.</param>
    [Theory]
    [InlineData("Baggage Handler")]
    [InlineData("Pilot")]
    public async Task Report_RejectsUsersWhoAreNotSupervisors(string role)
    {
        var result = await CreatePage(role).OnGetAsync();

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>Returns an empty report when the manifest has no flights.</summary>
    [Fact]
    public async Task Report_IsEmptyWhenNothingHasBeenImported()
    {
        var page = CreatePage();

        var result = await page.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(page.Reports);
        Assert.NotEqual(default, page.GeneratedAtUtc);
    }

    /// <summary>Includes a completed flight and counts every loaded bag.</summary>
    [Fact]
    public async Task Report_IncludesCompletedFlightWithLoadedBags()
    {
        _db.Flights.Add(Flight(1, "Complete"));
        _db.Bags.AddRange(Bag(1, 1, "Complete"), Bag(2, 1, "Complete"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        var report = Assert.Single(page.Reports);
        Assert.Equal(1, report.Flight.FlightId);
        Assert.Equal(2, report.Total);
        Assert.Equal(2, report.Loaded);
        Assert.Equal(0, report.Unloaded);
        Assert.Empty(report.Exceptions);
        Assert.Equal(0, report.ExceptionBags);
    }

    /// <summary>Leaves out a flight whose loading status is still started.</summary>
    [Fact]
    public async Task Report_ExcludesFlightThatIsNotComplete()
    {
        _db.Flights.AddRange(Flight(1, "Started"), Flight(2, "Complete"));
        _db.Bags.AddRange(Bag(1, 1, "Complete"), Bag(2, 2, "Complete"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        Assert.Equal(2, Assert.Single(page.Reports).Flight.FlightId);
    }

    /// <summary>Leaves out a completed flight that has no bags recorded.</summary>
    [Fact]
    public async Task Report_ExcludesCompletedFlightWithNoBags()
    {
        _db.Flights.Add(Flight(1, "Complete"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        Assert.Empty(page.Reports);
    }

    /// <summary>Leaves out a completed flight while any of its exceptions are still open.</summary>
    [Fact]
    public async Task Report_ExcludesFlightWithAnOpenException()
    {
        _db.Flights.Add(Flight(1, "Complete"));
        _db.Bags.Add(Bag(1, 1, "Complete"));
        _db.Exceptions.Add(Exception(1, 1, 1, "Open"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        Assert.Empty(page.Reports);
    }

    /// <summary>Includes approved damage and counts each affected bag once.</summary>
    [Fact]
    public async Task Report_CountsDistinctBagsForApprovedExceptions()
    {
        _db.Flights.Add(Flight(1, "Complete"));
        _db.Bags.AddRange(Bag(1, 1, "Complete"), Bag(2, 1, "Complete"));
        _db.Exceptions.AddRange(
            Exception(1, 1, 1, "ApprovedToLoad"),
            Exception(2, 1, 1, "ApprovedToLoad"),
            Exception(3, 2, 1, "ApprovedToLoad"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        var report = Assert.Single(page.Reports);
        Assert.Equal(3, report.Exceptions.Count);
        Assert.Equal(2, report.ExceptionBags);
        Assert.Equal(2, report.Loaded);
    }

    /// <summary>Counts a supervisor's keep-unloaded decision separately from loaded bags.</summary>
    [Fact]
    public async Task Report_CountsKeptUnloadedBags()
    {
        _db.Flights.Add(Flight(1, "Complete"));
        _db.Bags.AddRange(Bag(1, 1, "Complete"), Bag(2, 1, "Unloaded"));
        _db.Exceptions.Add(Exception(1, 2, 1, "KeptUnloaded"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        var report = Assert.Single(page.Reports);
        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.Loaded);
        Assert.Equal(1, report.Unloaded);
    }

    /// <summary>Leaves out an unloaded bag whose exception was not kept unloaded.</summary>
    [Fact]
    public async Task Report_ExcludesUnloadedBagWithoutKeptUnloadedDecision()
    {
        _db.Flights.Add(Flight(1, "Complete"));
        _db.Bags.Add(Bag(1, 1, "Unloaded"));
        _db.Exceptions.Add(Exception(1, 1, 1, "ApprovedToLoad"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        Assert.Empty(page.Reports);
    }

    /// <summary>Lists finalised flights by departure time and then by flight id.</summary>
    [Fact]
    public async Task Report_OrdersFlightsByDepartureThenFlightId()
    {
        var sameTime = new DateTime(2026, 10, 23, 2, 0, 0, DateTimeKind.Utc);
        _db.Flights.AddRange(
            Flight(20, "Complete", sameTime),
            Flight(10, "Complete", sameTime),
            Flight(30, "Complete", new DateTime(2026, 10, 23, 1, 0, 0, DateTimeKind.Utc)));
        _db.Bags.AddRange(Bag(1, 30, "Complete"), Bag(2, 10, "Complete"), Bag(3, 20, "Complete"));
        await _db.SaveChangesAsync();

        var page = CreatePage();
        await page.OnGetAsync();

        Assert.Equal([30, 10, 20], page.Reports.Select(report => report.Flight.FlightId).ToArray());
    }

    /// <summary>Creates the report page with the test database and an optional role.</summary>
    /// <param name="role">Role to authenticate; null creates a signed-out user.</param>
    private SummaryReportModel CreatePage(string? role = "BG_Supervisor")
    {
        var identity = role is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.Name, "supervisor@example.com")],
                "Test");
        return new SummaryReportModel(_db)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    /// <summary>Builds a flight with only the fields the report reads.</summary>
    private static Flight Flight(int id, string status, DateTime? departure = null) => new()
    {
        FlightId = id,
        FlightNumber = "QF" + id,
        LoadingStatus = status,
        ScheduledDepartureUtc = departure ?? new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>Builds a bag with the outcome the report uses to decide whether it is resolved.</summary>
    private static Bag Bag(int id, int flightId, string outcome) => new()
    {
        BagId = id,
        FlightId = flightId,
        Outcome = outcome
    };

    /// <summary>Builds an exception whose status determines whether the flight can be reported.</summary>
    private static BaggageException Exception(int id, int bagId, int flightId, string status) => new()
    {
        ExceptionId = id,
        BagId = bagId,
        FlightId = flightId,
        Category = "DamagedBag",
        Description = "Cracked shell",
        Status = status
    };

    /// <summary>Closes the connection and releases this case's database.</summary>
    public void Dispose() => _db.Dispose();
}
