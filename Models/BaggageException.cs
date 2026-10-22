using System.ComponentModel.DataAnnotations;

namespace AeroLink.Models
{
    /// Represents a reported baggage problem or exception during departure loading (User Story 58).
    public class BaggageException
    {
        /// Gets or sets the unique identifier for the baggage exception
        [Key]
        public int ExceptionId { get; set; }

        /// Gets or sets the foreign key reference to the affected bag
        [Required]
        public int BagId { get; set; }
        public Bag? Bag { get; set; }

        /// Gets or sets the foreign key reference to the departure flight
        [Required]
        public int FlightId { get; set; }
        public Flight? Flight { get; set; }

        /// Gets or sets the category of the exception
        [Required]
        [StringLength(50)]
        public string Category { get; set; } = "DamagedBag";

        /// Gets or sets the detailed comments or problem description provided by the baggage handler
        [Required]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string Description { get; set; } = string.Empty;

        /// Gets or sets the current review status of the exception
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Open";

        /// Gets or sets the supervisor's resolution notes or decision reason
        public string? DecisionReason { get; set; }

        /// Gets or sets the UTC timestamp when the exception was initially reported
        public DateTime ReportedTimestamp { get; set; } = DateTime.UtcNow;
    }
}