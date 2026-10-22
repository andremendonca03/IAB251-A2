namespace AeroLink.Models;

/// <summary>A manifest flight, including its route, scheduled departure and loading progress.</summary>
public class Flight
{
    public int FlightId { get; set; }
    public string FlightLabel { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string DepartureAirport { get; set; } = string.Empty;
    public string ArrivalAirport { get; set; } = string.Empty;
    /// <summary>Scheduled departure expressed in UTC.</summary>
    public DateTime ScheduledDepartureUtc { get; set; }
    public string AircraftId { get; set; } = string.Empty;
    /// <summary>Imported status, then Started or Complete as baggage decisions are saved.</summary>
    public string LoadingStatus { get; set; } = string.Empty;
}
