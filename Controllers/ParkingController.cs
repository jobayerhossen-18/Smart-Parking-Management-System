using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;

namespace SmartParkingMS.Controllers
{
    // Implements Section 13 (Check-In), Section 14 (Check-Out),
    // Section 15 (Fee Calculation), and Section 17 (Parking History)
    [Authorize]
    public class ParkingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private const int MinimumChargeHours = 1;

        public ParkingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Parking/CheckIn/5  (5 = BookingID)
        public async Task<IActionResult> CheckIn(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .Include(b => b.Vehicle)
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null) return NotFound();

            if (User.IsInRole(Roles.Customer) && booking.UserID != _userManager.GetUserId(User))
                return Forbid();

            if (booking.Status != BookingStatus.Confirmed)
            {
                TempData["ErrorMessage"] = "Only confirmed bookings can be checked in.";
                return RedirectToAction("Details", "Booking", new { id });
            }

            return View(booking);
        }

        // POST: /Parking/CheckIn/5
        [HttpPost, ActionName("CheckIn")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckInConfirmed(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && booking.UserID != _userManager.GetUserId(User))
                return Forbid();

            if (booking.Status != BookingStatus.Confirmed)
            {
                TempData["ErrorMessage"] = "Only confirmed bookings can be checked in.";
                return RedirectToAction("Details", "Booking", new { id });
            }

            var session = new ParkingSession
            {
                BookingID = booking.BookingID,
                CheckInTime = DateTime.Now
            };
            _context.ParkingSessions.Add(session);

            // After successful check-in: Slot Status -> Occupied, Booking Status -> Active(Confirmed/In-progress)
            if (booking.ParkingSlot != null)
                booking.ParkingSlot.Status = SlotStatus.Occupied;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Vehicle checked in successfully.";
            return RedirectToAction("Details", "Booking", new { id });
        }

        // GET: /Parking/CheckOut/5
        public async Task<IActionResult> CheckOut(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .Include(b => b.ParkingSession)
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && booking.UserID != _userManager.GetUserId(User))
                return Forbid();

            if (booking.ParkingSession?.CheckInTime == null)
            {
                TempData["ErrorMessage"] = "This vehicle has not checked in yet.";
                return RedirectToAction("Details", "Booking", new { id });
            }

            // Estimate fee for preview purposes (section 15)
            var duration = DateTime.Now - booking.ParkingSession.CheckInTime.Value;
            var hours = Math.Max(MinimumChargeHours, Math.Ceiling(duration.TotalHours));
            var rate = booking.ParkingSlot?.HourlyRate ?? 0;
            ViewBag.EstimatedFee = (decimal)hours * rate;
            ViewBag.EstimatedHours = hours;

            return View(booking);
        }

        // POST: /Parking/CheckOut/5
        [HttpPost, ActionName("CheckOut")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOutConfirmed(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .Include(b => b.ParkingSession)
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && booking.UserID != _userManager.GetUserId(User))
                return Forbid();

            if (booking.ParkingSession?.CheckInTime == null)
            {
                TempData["ErrorMessage"] = "This vehicle has not checked in yet.";
                return RedirectToAction("Details", "Booking", new { id });
            }

            var checkOutTime = DateTime.Now;
            var duration = checkOutTime - booking.ParkingSession.CheckInTime.Value;

            // Section 15: Parking Fee = Parking Duration x Hourly Rate, with a minimum charge (1 hour)
            var billableHours = Math.Max(MinimumChargeHours, Math.Ceiling(duration.TotalHours));
            var rate = booking.ParkingSlot?.HourlyRate ?? 0;
            var fee = (decimal)billableHours * rate;

            booking.ParkingSession.CheckOutTime = checkOutTime;
            booking.ParkingSession.DurationMinutes = duration.TotalMinutes;
            booking.ParkingSession.ParkingFee = fee;

            // IMPORTANT: checkout calculates the bill but does not release the slot yet.
            // The vehicle/slot remains locked until payment is successfully confirmed.
            booking.Status = BookingStatus.Confirmed;

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == booking.BookingID);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = booking.BookingID,
                    UserID = booking.UserID,
                    Amount = fee,
                    PaymentMethod = PaymentMethod.Offline,
                    PaymentStatus = PaymentStatus.Pending,
                    PaymentDate = DateTime.Now
                };
                _context.Payments.Add(payment);
            }
            else
            {
                payment.Amount = fee;
                payment.PaymentStatus = PaymentStatus.Pending;
                payment.PaymentMethod = PaymentMethod.Offline;
                payment.PaymentDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Parking ended. Pay {fee:0.00} BDT to release the vehicle and parking slot.";
            return RedirectToAction("Pay", "Payment", new { id = payment.PaymentID });
        }

        // GET: /Parking/History  (Section 17: Parking History)
        public async Task<IActionResult> History()
        {
            IQueryable<Booking> query = _context.Bookings
                .Include(b => b.Vehicle)
                .Include(b => b.ParkingSlot)
                .Include(b => b.ParkingSession)
                .Include(b => b.Payment)
                .Include(b => b.User)
                .Where(b => b.Status == BookingStatus.Completed);

            if (User.IsInRole(Roles.Customer))
            {
                var userId = _userManager.GetUserId(User);
                query = query.Where(b => b.UserID == userId);
            }
            // Administrators shall be able to view the parking history of all users (no filter for Admin).

            var history = await query.OrderByDescending(b => b.ParkingSession!.CheckOutTime).ToListAsync();
            return View(history);
        }
    }
}
