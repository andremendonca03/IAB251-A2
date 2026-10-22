using AeroLink.Data;
using AeroLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AeroLink.Pages
{
    /// Page model handling the reporting of baggage exceptions by handlers
    public class ReportExceptionModel : PageModel
    {
        private readonly BaggageHandlingContext _context;

        public ReportExceptionModel(BaggageHandlingContext context)
        {
            _context = context;
        }

        /// Gets or sets the target departure flight ID passed via route parameters
        [BindProperty(SupportsGet = true)]
        public int FlightId { get; set; }

        /// Gets or sets the selected bag ID that has an identified problem
        [BindProperty]
        [Required(ErrorMessage = "Please select a bag to report.")]
        public int BagId { get; set; }

        /// <summary>
        /// Gets or sets the category of the problem selected by the handler.
        [BindProperty]
        [Required]
        public string Category { get; set; } = "DamagedBag";

        /// Gets or sets the handler's descriptive comments regarding the issue
        [BindProperty]
        [Required(ErrorMessage = "Please provide details about the identified problem.")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string Description { get; set; } = string.Empty;

        /// Gets or sets the collection of bags associated with the flight manifest
        public IList<Bag> ManifestBags { get; set; } = new List<Bag>();

        /// Gets or sets the current flight details being viewed
        public Flight? CurrentFlight { get; set; }

        /// Handles HTTP GET requests to load manifest bags for the specified flight
        public async Task<IActionResult> OnGetAsync()
        {
            // Verify flight existence
            CurrentFlight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightId == FlightId);

            if (CurrentFlight == null)
            {
                return RedirectToPage("/baggage-handling");
            }

            // Load bags belonging to this specific flight manifest
            ManifestBags = await _context.Bags
                .Where(b => b.FlightId == FlightId)
                .ToListAsync();

            return Page();
        }

        /// Handles HTTP POST requests to validate and save a new open baggage exception report
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Reload flight and bag data if validation fails
                CurrentFlight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightId == FlightId);
                ManifestBags = await _context.Bags
                    .Where(b => b.FlightId == FlightId)
                    .ToListAsync();
                return Page();
            }

            // Create the exception entity flagged as Open
            var baggageException = new BaggageException
            {
                BagId = BagId,
                FlightId = FlightId,
                Category = Category,
                Description = Description,
                Status = "Open",
                ReportedTimestamp = DateTime.UtcNow
            };

            // Save exception to database to block premature flight loading completion
            _context.Exceptions.Add(baggageException);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Baggage problem reported successfully. Exception flagged for supervisor review.";

            return RedirectToPage("/baggage-handling", new { flightId = FlightId });
        }
    }
}