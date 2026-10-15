using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;

namespace SmartParkingMS.Controllers
{
    // Implements Section 20: Report Management (AR-14)
    [Authorize(Roles = Roles.Admin)]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Report?from=...&to=...  (Reports may be filtered by date)
        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var paymentsQuery = _context.Payments.Include(p => p.Booking).AsQueryable();
            var bookingsQuery = _context.Bookings.AsQueryable();
            var sessionsQuery = _context.ParkingSessions.AsQueryable();

            if (from.HasValue)
            {
                paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= from.Value);
                bookingsQuery = bookingsQuery.Where(b => b.CreatedAt >= from.Value);
            }
            if (to.HasValue)
            {
                var toInclusive = to.Value.Date.AddDays(1);
                paymentsQuery = paymentsQuery.Where(p => p.PaymentDate < toInclusive);
                bookingsQuery = bookingsQuery.Where(b => b.CreatedAt < toInclusive);
            }

            var vm = new ReportViewModel
            {
                // User Report
                TotalRegisteredUsers = await _userManager.Users.CountAsync(),

                // Parking Report
                TotalParkingSessions = await sessionsQuery.CountAsync(s => s.CheckOutTime != null),
                OccupiedSlots = await _context.ParkingSlots.CountAsync(s => s.Status == SlotStatus.Occupied),
                AvailableSlots = await _context.ParkingSlots.CountAsync(s => s.Status == SlotStatus.Available),

                // Booking Report
                TotalBookings = await bookingsQuery.CountAsync(),
                CompletedBookings = await bookingsQuery.CountAsync(b => b.Status == BookingStatus.Completed),
                CancelledBookings = await bookingsQuery.CountAsync(b => b.Status == BookingStatus.Cancelled),

                // Revenue Report
                TotalRevenueCollected = await paymentsQuery.Where(p => p.PaymentStatus == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0,
                PaymentRecords = await paymentsQuery.OrderByDescending(p => p.PaymentDate).Take(100).ToListAsync(),

                FromDate = from,
                ToDate = to
            };

            return View(vm);
        }
    }
}
