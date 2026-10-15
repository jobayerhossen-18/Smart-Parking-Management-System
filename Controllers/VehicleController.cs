using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;

namespace SmartParkingMS.Controllers
{
    // Implements Section 8: Vehicle Management (CR-05, CR-06, CR-07)
    // Business rule: Users can access only their own vehicles.
    [Authorize(Roles = Roles.Customer)]
    public class VehicleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public VehicleController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Vehicle
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var vehicles = await _context.Vehicles
                .Where(v => v.UserID == userId)
                .ToListAsync();
            return View(vehicles);
        }

        // GET: /Vehicle/Create
        public IActionResult Create() => View(new Vehicle());

        // POST: /Vehicle/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Vehicle vehicle)
        {
            var userId = _userManager.GetUserId(User);

            // Business rule: registration number must be unique in the system
            bool duplicate = await _context.Vehicles.AnyAsync(v => v.RegistrationNumber == vehicle.RegistrationNumber);
            if (duplicate)
                ModelState.AddModelError(nameof(Vehicle.RegistrationNumber), "This registration number is already registered.");

            ModelState.Remove(nameof(Vehicle.UserID));
            ModelState.Remove(nameof(Vehicle.User));

            if (!ModelState.IsValid)
                return View(vehicle);

            vehicle.UserID = userId!;
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Vehicle added successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Vehicle/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleID == id && v.UserID == userId);
            if (vehicle == null) return NotFound();
            return View(vehicle);
        }

        // POST: /Vehicle/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Vehicle vehicle)
        {
            var userId = _userManager.GetUserId(User);
            var existing = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleID == id && v.UserID == userId);
            if (existing == null) return NotFound();

            bool duplicate = await _context.Vehicles.AnyAsync(v => v.RegistrationNumber == vehicle.RegistrationNumber && v.VehicleID != id);
            if (duplicate)
                ModelState.AddModelError(nameof(Vehicle.RegistrationNumber), "This registration number is already registered.");

            ModelState.Remove(nameof(Vehicle.UserID));
            ModelState.Remove(nameof(Vehicle.User));

            if (!ModelState.IsValid)
                return View(vehicle);

            existing.RegistrationNumber = vehicle.RegistrationNumber;
            existing.VehicleType = vehicle.VehicleType;
            existing.Model = vehicle.Model;
            existing.Color = vehicle.Color;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Vehicle updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Vehicle/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleID == id && v.UserID == userId);
            if (vehicle == null) return NotFound();
            return View(vehicle);
        }

        // POST: /Vehicle/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleID == id && v.UserID == userId);
            if (vehicle == null) return NotFound();

            bool hasActiveBooking = await _context.Bookings.AnyAsync(b => b.VehicleID == id &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            if (hasActiveBooking)
            {
                TempData["ErrorMessage"] = "Cannot delete a vehicle with an active booking.";
                return RedirectToAction(nameof(Index));
            }

            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Vehicle deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
