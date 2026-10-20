namespace AeroLink.Models;

public class Bag
{
    public int BagId { get; set; }
    public int FlightId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string HandlingType { get; set; } = string.Empty;
    public string HandlingInstruction { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
}
