using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartParkingMS.Models
{
    // Corresponds to requirement analysis sections 13-15 (Check-In, Check-Out, Fee Calculation)
    // and section 26 (ParkingSessions table)
    public class ParkingSession
    {
        [Key]
        public int SessionID { get; set; }

        [Required]
        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        public DateTime? CheckInTime { get; set; }

        public DateTime? CheckOutTime { get; set; }

        // Duration stored in minutes for precision; displayed as hours in views
        public double? DurationMinutes { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ParkingFee { get; set; }
    }
}
