using System.ComponentModel.DataAnnotations;

namespace SmartParkingMS.Models
{
    // Possible slot statuses per requirement analysis section 9
    public enum SlotStatus
    {
        Available,
        Reserved,
        Occupied,
        Maintenance
    }

    // Corresponds to requirement analysis section 9 (Parking Slot Management) and section 26 (ParkingSlots table)
    public class ParkingSlot
    {
        [Key]
        public int SlotID { get; set; }

        [Required]
        [Display(Name = "Slot Number")]
        public string SlotNumber { get; set; } = string.Empty;

        [Display(Name = "Floor/Zone")]
        public string Floor { get; set; } = string.Empty;

        [Display(Name = "Vehicle Type")]
        public VehicleType VehicleType { get; set; }

        public SlotStatus Status { get; set; } = SlotStatus.Available;

        [Display(Name = "Hourly Rate (BDT)")]
        [Range(0, 100000)]
        public decimal HourlyRate { get; set; }

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
