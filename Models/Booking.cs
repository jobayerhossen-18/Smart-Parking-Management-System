using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartParkingMS.Models
{
    // Booking status per requirement analysis section 11
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Cancelled,
        Completed
    }

    // Corresponds to requirement analysis section 11 (Parking Slot Booking) and section 26 (Bookings table)
    public class Booking
    {
        [Key]
        public int BookingID { get; set; }

        [Required]
        public string UserID { get; set; } = string.Empty;
        [ForeignKey("UserID")]
        public ApplicationUser? User { get; set; }

        [Required]
        public int VehicleID { get; set; }
        [ForeignKey("VehicleID")]
        public Vehicle? Vehicle { get; set; }

        [Required]
        public int SlotID { get; set; }
        [ForeignKey("SlotID")]
        public ParkingSlot? ParkingSlot { get; set; }

        [Required]
        [Display(Name = "Booking Date")]
        [DataType(DataType.Date)]
        public DateTime BookingDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Expected Entry Time")]
        public DateTime ExpectedEntryTime { get; set; }

        [Required]
        [Display(Name = "Expected Exit Time")]
        public DateTime ExpectedExitTime { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ParkingSession? ParkingSession { get; set; }
        public Payment? Payment { get; set; }
    }
}
