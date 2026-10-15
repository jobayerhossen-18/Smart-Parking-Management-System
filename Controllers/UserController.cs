using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartParkingMS.Data;
using SmartParkingMS.Models;

namespace SmartParkingMS.Controllers
{
    // Customer Dashboard (Section 5.2 / FR-02 post-login landing page)
    [Authorize(Roles = Roles.Customer)]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /User  -> Customer Dashboard
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            ViewBag.FullName = user.FullName;
            ViewBag.VehicleCount = _context.Vehicles.Count(v => v.UserID == user.Id);
            ViewBag.ActiveBookings = _context.Bookings.Count(b => b.UserID == user.Id &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            ViewBag.CompletedBookings = _context.Bookings.Count(b => b.UserID == user.Id && b.Status == BookingStatus.Completed);

            return View();
        }
    }
}
