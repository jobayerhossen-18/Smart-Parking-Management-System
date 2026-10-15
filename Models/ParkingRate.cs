using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartParkingMS.Models
{
    // Supports section 5.1 (Admin can manage parking rates / AR-08)
    // Allows the admin to set a default hourly rate per vehicle type,
    // separate from the per-slot HourlyRate override on ParkingSlot.
    public class ParkingRate
    {
        [Key]
        public int RateID { get; set; }

        [Required]
        [Display(Name = "Vehicle Type")]
        public VehicleType VehicleType { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Hourly Rate (BDT)")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Minimum Charge Hours")]
        public int MinimumChargeHours { get; set; } = 1;
    }
}
