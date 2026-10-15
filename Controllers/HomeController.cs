using Microsoft.AspNetCore.Mvc;
using SmartParkingMS.Data;

namespace SmartParkingMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /  (public landing page)
        public IActionResult Index()
        {
            ViewBag.AvailableSlots = _context.ParkingSlots.Count(s => s.Status == Models.SlotStatus.Available);
            ViewBag.TotalSlots = _context.ParkingSlots.Count();
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
