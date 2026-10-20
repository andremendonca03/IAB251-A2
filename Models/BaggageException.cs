using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AeroLink.Models;

[Table("Exceptions")]
public class BaggageException
{
    [Key]
    public int ExceptionId { get; set; }
    public int BagId { get; set; }
    public int FlightId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
}
