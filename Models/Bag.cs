namespace AeroLink.Models;

/// <summary>A manifest bag with its handling instructions and current loading outcome.</summary>
public class Bag
{
    public int BagId { get; set; }
    public int FlightId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string HandlingType { get; set; } = string.Empty;
    public string HandlingInstruction { get; set; } = string.Empty;
    /// <summary>Pending, Complete, Missing, Damaged, or Unloaded after supervisor approval.</summary>
    public string Outcome { get; set; } = string.Empty;
}
