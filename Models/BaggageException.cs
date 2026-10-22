using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AeroLink.Models;

/// <summary>A reported baggage issue and its supervisor's loading decision.</summary>
[Table("Exceptions")]
public class BaggageException
{
    [Key]
    public int ExceptionId { get; set; }
    public int BagId { get; set; }
    public int FlightId { get; set; }
    public string Category { get; set; } = string.Empty;
    /// <summary>Reported issue description, preserved when the supervisor approves a decision.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Open while awaiting review; ApprovedToLoad or KeptUnloaded after approval.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Decision, supervisor identity and justification; cleared when the issue is reopened.</summary>
    public string DecisionReason { get; set; } = string.Empty;
}
