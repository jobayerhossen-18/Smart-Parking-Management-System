
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Models;

namespace SmartParkingMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Vehicle> Vehicles { get; set; } = default!;
        public DbSet<ParkingSlot> ParkingSlots { get; set; } = default!;
        public DbSet<Booking> Bookings { get; set; } = default!;
        public DbSet<ParkingSession> ParkingSessions { get; set; } = default!;
        public DbSet<Payment> Payments { get; set; } = default!;
        public DbSet<ParkingRate> ParkingRates { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Vehicle registration numbers must be unique.
            builder.Entity<Vehicle>()
                .HasIndex(v => v.RegistrationNumber)
                .IsUnique();

            // Slot numbers must be unique.
            builder.Entity<ParkingSlot>()
                .HasIndex(s => s.SlotNumber)
                .IsUnique();

            // Configure ParkingSlot hourly rate precision.
            builder.Entity<ParkingSlot>()
                .Property(p => p.HourlyRate)
                .HasPrecision(18, 2);

            // Booking - ParkingSession (one-to-one relationship).
            builder.Entity<Booking>()
                .HasOne(b => b.ParkingSession)
                .WithOne(ps => ps.Booking)
                .HasForeignKey<ParkingSession>(ps => ps.BookingID)
                .OnDelete(DeleteBehavior.Cascade);

            // Booking - Payment (one-to-one relationship).
            builder.Entity<Booking>()
                .HasOne(b => b.Payment)
                .WithOne(p => p.Booking)
                .HasForeignKey<Payment>(p => p.BookingID)
                .OnDelete(DeleteBehavior.Cascade);

            // Prevent unintended cascade deletes.
            builder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Booking>()
                .HasOne(b => b.Vehicle)
                .WithMany(v => v.Bookings)
                .HasForeignKey(b => b.VehicleID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Booking>()
                .HasOne(b => b.ParkingSlot)
                .WithMany(s => s.Bookings)
                .HasForeignKey(b => b.SlotID)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment - User relationship.
            builder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            // Vehicle - User relationship.
            builder.Entity<Vehicle>()
                .HasOne(v => v.User)
                .WithMany(u => u.Vehicles)
                .HasForeignKey(v => v.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure payment amount precision.
            builder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(10,2)");
        }
    }
}