using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;
using SmartParkingMS.Services;

namespace SmartParkingMS.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SslCommerzService _gateway;
        private readonly IWebHostEnvironment _environment;

        public PaymentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SslCommerzService gateway, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _gateway = gateway;
            _environment = environment;
        }

        public async Task<IActionResult> Pay(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)!.ThenInclude(b => b!.ParkingSlot)
                .Include(p => p.Booking)!.ThenInclude(b => b!.Vehicle)
                .FirstOrDefaultAsync(p => p.PaymentID == id);
            if (payment == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && payment.UserID != _userManager.GetUserId(User)) return Forbid();
            if (payment.PaymentStatus == PaymentStatus.Paid)
                return RedirectToAction(nameof(Receipt), new { id = payment.PaymentID });
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartOnline(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)!.ThenInclude(b => b!.ParkingSlot)
                .FirstOrDefaultAsync(p => p.PaymentID == id);
            if (payment == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && payment.UserID != _userManager.GetUserId(User)) return Forbid();

            payment.PaymentMethod = PaymentMethod.Online;
            payment.PaymentStatus = PaymentStatus.Pending;
            await _context.SaveChangesAsync();

            var customer = await _userManager.FindByIdAsync(payment.UserID);
            var gateway = await _gateway.InitiateAsync(
                payment.PaymentID,
                payment.Amount,
                customer?.FullName ?? "Smart Parking Customer",
                customer?.Email ?? "",
                payment.BookingID.ToString());

            if (gateway.Success && !string.IsNullOrWhiteSpace(gateway.GatewayPageUrl))
                return Redirect(gateway.GatewayPageUrl);

            TempData["GatewayMessage"] = gateway.ErrorMessage ?? "Gateway credentials are not configured. Demo sandbox is available for local testing.";
            return RedirectToAction(nameof(OnlineGateway), new { id = payment.PaymentID });
        }

        public async Task<IActionResult> OnlineGateway(int id)
        {
            var payment = await LoadPayment(id);
            if (payment == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && payment.UserID != _userManager.GetUserId(User)) return Forbid();
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteDemoOnline(int id, string provider)
        {
            // This is a clearly labelled local demo only. It does not contact bKash, Nagad, or a bank
            // and must never be used to represent a real-money transaction. Production is blocked.
            if (!_environment.IsDevelopment())
                return NotFound();

            var allowedProviders = new[] { "bKash", "Nagad", "Bank Card" };
            if (string.IsNullOrWhiteSpace(provider) || !allowedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Please select a valid demo payment method.";
                return RedirectToAction(nameof(OnlineGateway), new { id });
            }

            var payment = await LoadPayment(id);
            if (payment == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && payment.UserID != _userManager.GetUserId(User)) return Forbid();

            payment.PaymentMethod = PaymentMethod.Online;
            payment.PaymentStatus = PaymentStatus.Paid;
            payment.PaymentDate = DateTime.Now;
            await ReleaseAfterPayment(payment);
            TempData["PaymentProvider"] = allowedProviders.First(p => p.Equals(provider, StringComparison.OrdinalIgnoreCase));
            TempData["DemoPayment"] = true;
            TempData["SuccessMessage"] = "Demo payment successful. No real money was charged. Your receipt is ready.";
            return RedirectToAction(nameof(Receipt), new { id = payment.PaymentID });
        }

        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var payment = await LoadPayment(id);
            if (payment == null) return NotFound();
            if (payment.PaymentStatus == PaymentStatus.Paid) return RedirectToAction(nameof(Receipt), new { id = payment.PaymentID });
            if (payment.PaymentMethod == PaymentMethod.Online && payment.PaymentStatus == PaymentStatus.Failed)
            {
                TempData["ErrorMessage"] = "This online payment failed. The customer must retry online payment.";
                return RedirectToAction(nameof(PaymentHistory));
            }

            payment.PaymentMethod = PaymentMethod.Offline;
            payment.PaymentStatus = PaymentStatus.Paid;
            payment.PaymentDate = DateTime.Now;
            await ReleaseAfterPayment(payment);

            TempData["SuccessMessage"] = "Offline payment confirmed. The vehicle can now exit and the slot is available.";
            return RedirectToAction(nameof(PaymentHistory));
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GatewaySuccess()
        {
            var tranId = Request.Form["tran_id"].ToString();
            var valId = Request.Form["val_id"].ToString();
            if (!TryGetPaymentId(tranId, out var paymentId) || string.IsNullOrWhiteSpace(valId)) return RedirectToAction(nameof(PaymentHistory));

            var payment = await LoadPayment(paymentId);
            if (payment == null) return NotFound();
            var result = await _gateway.ValidateAsync(valId);
            if (!result.Success || Math.Abs(result.Amount - payment.Amount) > 0.01m ||
                !string.Equals(result.TransactionId, $"SPPAY-{payment.PaymentID}", StringComparison.Ordinal) ||
                !string.Equals(result.Currency, "BDT", StringComparison.OrdinalIgnoreCase))
            {
                payment.PaymentStatus = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = "Online payment validation failed. The slot remains locked until payment is verified.";
                return RedirectToAction(nameof(Pay), new { id = payment.PaymentID });
            }

            payment.PaymentMethod = PaymentMethod.Online;
            payment.PaymentStatus = PaymentStatus.Paid;
            payment.PaymentDate = DateTime.Now;
            await ReleaseAfterPayment(payment);
            return RedirectToAction(nameof(Receipt), new { id = payment.PaymentID });
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GatewayFail()
        {
            var tranId = Request.Form["tran_id"].ToString();
            if (string.IsNullOrWhiteSpace(tranId)) tranId = Request.Query["tran_id"].ToString();
            if (TryGetPaymentId(tranId, out var paymentId))
            {
                var payment = await _context.Payments.FindAsync(paymentId);
                if (payment != null) { payment.PaymentStatus = PaymentStatus.Failed; await _context.SaveChangesAsync(); }
            }
            TempData["ErrorMessage"] = "Online payment was not completed. Your parking slot remains locked.";
            return RedirectToAction(nameof(PaymentHistory));
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GatewayCancel()
        {
            TempData["ErrorMessage"] = "Online payment was cancelled. Complete payment before the vehicle can exit or the slot can be released.";
            return RedirectToAction(nameof(PaymentHistory));
        }

        private static bool TryGetPaymentId(string? tranId, out int paymentId)
        {
            paymentId = 0;
            if (string.IsNullOrWhiteSpace(tranId) || !tranId.StartsWith("SPPAY-", StringComparison.OrdinalIgnoreCase)) return false;
            return int.TryParse(tranId[6..], out paymentId);
        }

        public async Task<IActionResult> Receipt(int id)
        {
            var payment = await LoadPayment(id);
            if (payment == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && payment.UserID != _userManager.GetUserId(User)) return Forbid();
            if (payment.PaymentStatus != PaymentStatus.Paid)
                return RedirectToAction(nameof(Pay), new { id });
            return View(payment);
        }

        public async Task<IActionResult> PaymentHistory()
        {
            IQueryable<Payment> query = _context.Payments
                .Include(p => p.Booking)!.ThenInclude(b => b!.ParkingSlot)
                .Include(p => p.Booking)!.ThenInclude(b => b!.Vehicle)
                .Include(p => p.User);

            if (User.IsInRole(Roles.Customer))
            {
                var userId = _userManager.GetUserId(User);
                query = query.Where(p => p.UserID == userId);
            }
            var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
            return View(payments);
        }

        public async Task<IActionResult> Details(int id)
        {
            var payment = await LoadPayment(id);
            if (payment == null) return NotFound();
            if (User.IsInRole(Roles.Customer) && payment.UserID != _userManager.GetUserId(User)) return Forbid();
            return View(payment);
        }

        private async Task<Payment?> LoadPayment(int id) => await _context.Payments
            .Include(p => p.Booking)!.ThenInclude(b => b!.ParkingSlot)
            .Include(p => p.Booking)!.ThenInclude(b => b!.Vehicle)
            .Include(p => p.Booking)!.ThenInclude(b => b!.ParkingSession)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PaymentID == id);

        private async Task ReleaseAfterPayment(Payment payment)
        {
            var booking = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .Include(b => b.ParkingSession)
                .FirstAsync(b => b.BookingID == payment.BookingID);

            payment.PaymentStatus = PaymentStatus.Paid;

            // A paid checkout releases the slot. For a confirmed reservation cancelled after confirmation,
            // the cancellation action uses the same payment mechanism and then releases the reservation.
            if (booking.ParkingSession?.CheckInTime != null)
            {
                // Normal parking checkout: payment unlocks the vehicle and completes the session.
                if (booking.ParkingSession.CheckOutTime == null)
                {
                    booking.ParkingSession.CheckOutTime = payment.PaymentDate;
                    if (booking.ParkingSession.DurationMinutes == null)
                        booking.ParkingSession.DurationMinutes = Math.Max(0, (booking.ParkingSession.CheckOutTime.Value - booking.ParkingSession.CheckInTime.Value).TotalMinutes);
                }
                booking.Status = BookingStatus.Completed;
            }
            else if (booking.Status == BookingStatus.Confirmed)
            {
                // No parking session means this was a confirmed reservation cancelled before check-in.
                booking.Status = BookingStatus.Cancelled;
            }

            if (booking.ParkingSlot != null)
                booking.ParkingSlot.Status = SlotStatus.Available;

            await _context.SaveChangesAsync();
        }
    }
}
