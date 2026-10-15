using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;

namespace SmartParkingMS.Controllers
{
    // Implements Section 18 (Admin Dashboard) and AR-03 (Admin can manage users)
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Admin/Dashboard  (Section 18)
        public async Task<IActionResult> Dashboard()
        {
            var vm = new AdminDashboardViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalVehicles = await _context.Vehicles.CountAsync(),
                TotalParkingSlots = await _context.ParkingSlots.CountAsync(),
                AvailableSlots = await _context.ParkingSlots.CountAsync(s => s.Status == SlotStatus.Available),
                ReservedSlots = await _context.ParkingSlots.CountAsync(s => s.Status == SlotStatus.Reserved),
                OccupiedSlots = await _context.ParkingSlots.CountAsync(s => s.Status == SlotStatus.Occupied),
                TotalBookings = await _context.Bookings.CountAsync(),
                CompletedBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Completed),
                CancelledBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Cancelled),
                PendingPayments = await _context.Payments.CountAsync(p => p.PaymentStatus == PaymentStatus.Pending),
                ActiveParking = await _context.ParkingSessions.CountAsync(ps => ps.CheckInTime != null && ps.CheckOutTime == null),
                TotalRevenue = await _context.Payments.Where(p => p.PaymentStatus == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0
            };

            return View(vm);
        }

        // GET: /Admin/Users  (AR-03: Admin can manage users)
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            var userRoles = new Dictionary<string, string>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                userRoles[u.Id] = roles.FirstOrDefault() ?? "N/A";
            }
            ViewBag.UserRoles = userRoles;
            return View(users);
        }

        // GET: /Admin/UserDetails/{id}
        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.Vehicles = await _context.Vehicles.Where(v => v.UserID == id).ToListAsync();
            ViewBag.Bookings = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .Where(b => b.UserID == id)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            ViewBag.Roles = await _userManager.GetRolesAsync(user);

            return View(user);
        }

        // POST: /Admin/DeactivateUser/{id}  (lock account instead of hard delete, to preserve history)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (await _userManager.IsLockedOutAsync(user))
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["SuccessMessage"] = "User account re-activated.";
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                TempData["SuccessMessage"] = "User account deactivated.";
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: /Admin/Rates  (AR-08: Admin can manage parking rates)
        public async Task<IActionResult> Rates()
        {
            var rates = await _context.ParkingRates.ToListAsync();
            return View(rates);
        }

        // POST: /Admin/Rates
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRate(int rateId, decimal hourlyRate, int minimumChargeHours)
        {
            var rate = await _context.ParkingRates.FindAsync(rateId);
            if (rate == null) return NotFound();

            rate.HourlyRate = hourlyRate;
            rate.MinimumChargeHours = minimumChargeHours;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Parking rate updated.";
            return RedirectToAction(nameof(Rates));
        }
    }
}
