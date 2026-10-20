namespace AeroLink.Models;

public class Flight
{
    public int FlightId { get; set; }
    public string FlightLabel { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string DepartureAirport { get; set; } = string.Empty;
    public string ArrivalAirport { get; set; } = string.Empty;
    public DateTime ScheduledDepartureUtc { get; set; }
    public string AircraftId { get; set; } = string.Empty;
    public string LoadingStatus { get; set; } = string.Empty;
}
