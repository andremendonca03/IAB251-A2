using AeroLink.Models;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Data
{
    /// Database context for managing flight manifests, baggage items, and exceptions.
    public class BaggageHandlingContext : DbContext
    {
        public BaggageHandlingContext(DbContextOptions<BaggageHandlingContext> options) : base(options) { }

        public DbSet<Flight> Flights => Set<Flight>();
        public DbSet<Bag> Bags => Set<Bag>();

        /// DbSet for tracking reported baggage problems and exceptions
        public DbSet<BaggageException> Exceptions => Set<BaggageException>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Flight>().Property(flight => flight.FlightId).ValueGeneratedNever();
            modelBuilder.Entity<Bag>().Property(bag => bag.BagId).ValueGeneratedNever();
            modelBuilder.Entity<BaggageException>().Property(item => item.ExceptionId).ValueGeneratedNever();
        }
    }
}