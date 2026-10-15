using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartParkingMS.Models
{
    public enum PaymentMethod
    {
        Offline = 0,
        Online = 1
    }

    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed
    }

    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }

        [Required]
        public int BookingID { get; set; }
        [ForeignKey("BookingID")]
        public Booking? Booking { get; set; }

        [Required]
        public string UserID { get; set; } = string.Empty;
        [ForeignKey("UserID")]
        public ApplicationUser? User { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Offline;
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    }
}
