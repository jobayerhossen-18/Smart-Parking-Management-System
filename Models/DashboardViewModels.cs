namespace SmartParkingMS.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalVehicles { get; set; }
        public int TotalParkingSlots { get; set; }
        public int AvailableSlots { get; set; }
        public int ReservedSlots { get; set; }
        public int OccupiedSlots { get; set; }
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public int PendingPayments { get; set; }
        public int ActiveParking { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class ReportViewModel
    {
        public int TotalRegisteredUsers { get; set; }
        public int TotalParkingSessions { get; set; }
        public int OccupiedSlots { get; set; }
        public int AvailableSlots { get; set; }
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public decimal TotalRevenueCollected { get; set; }
        public List<Payment> PaymentRecords { get; set; } = new();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class ParkingHistoryItemViewModel
    {
        public int BookingID { get; set; }
        public string VehicleRegistration { get; set; } = string.Empty;
        public string SlotNumber { get; set; } = string.Empty;
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public double? DurationMinutes { get; set; }
        public decimal? ParkingFee { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
    }
}
