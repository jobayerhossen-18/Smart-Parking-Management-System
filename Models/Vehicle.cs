using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartParkingMS.Models
{
    public enum VehicleType
    {
        Car,
        Motorcycle,
        SUV,
        Other,
        CNG
    }

    // Corresponds to requirement analysis section 8 (Vehicle Management) and section 26 (Vehicles table)
    public class Vehicle
    {
        [Key]
        public int VehicleID { get; set; }

        [Required]
        public string UserID { get; set; } = string.Empty;

        [ForeignKey("UserID")]
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Vehicle registration number is required.")]
        [Display(Name = "Registration Number")]
        public string RegistrationNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Vehicle Type")]
        public VehicleType VehicleType { get; set; }

        [Display(Name = "Model")]
        public string? Model { get; set; }

        public string? Color { get; set; }

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
