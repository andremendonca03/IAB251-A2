using AeroLink.Models;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Data;

/// <summary>Stores the current flight manifest, bags and exception decisions in SQLite.</summary>
public class BaggageHandlingContext : DbContext
{
    /// <summary>Creates a context using the supplied database configuration.</summary>
    public BaggageHandlingContext(DbContextOptions<BaggageHandlingContext> options) : base(options) { }

    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Bag> Bags => Set<Bag>();
    public DbSet<BaggageException> Exceptions => Set<BaggageException>();

    /// <summary>Preserves workbook identifiers instead of generating new database IDs.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>().Property(flight => flight.FlightId).ValueGeneratedNever();
        modelBuilder.Entity<Bag>().Property(bag => bag.BagId).ValueGeneratedNever();
        modelBuilder.Entity<BaggageException>().Property(item => item.ExceptionId).ValueGeneratedNever();
    }
}
