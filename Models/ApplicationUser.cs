using Microsoft.AspNetCore.Identity;

namespace SmartParkingMS.Models
{
    // Corresponds to requirement analysis section 26: Users table
    // UserID (PK) -> Id (inherited from IdentityUser)
    // Name, Email, Phone, Password, Address, Role, CreatedAt
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Customer = "Customer";
    }
}
