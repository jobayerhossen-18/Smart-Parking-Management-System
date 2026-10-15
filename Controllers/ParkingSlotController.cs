using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;

namespace SmartParkingMS.Controllers
{
    // Implements Section 9 (Parking Slot Management, admin-only, AR-05..AR-07)
    // and Section 10 (Parking Slot Availability, visible to logged-in customers)
    [Authorize]
    public class ParkingSlotController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ParkingSlotController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ParkingSlot  (Admin: view all parking slots)
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Index()
        {
            var slots = await _context.ParkingSlots.OrderBy(s => s.SlotNumber).ToListAsync();
            return View(slots);
        }

        // GET: /ParkingSlot/AvailableSlots  (Customer: Section 10)
        [Authorize(Roles = Roles.Customer)]
        public async Task<IActionResult> AvailableSlots(VehicleType? vehicleType)
        {
            var query = _context.ParkingSlots.AsQueryable();
            if (vehicleType.HasValue)
                query = query.Where(s => s.VehicleType == vehicleType.Value);

            ViewBag.SelectedType = vehicleType;
            var slots = await query.OrderBy(s => s.SlotNumber).ToListAsync();
            return View(slots);
        }

        // GET: /ParkingSlot/Create
        [Authorize(Roles = Roles.Admin)]
        public IActionResult Create() => View(new ParkingSlot());

        // POST: /ParkingSlot/Create  (AR-05)
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ParkingSlot slot)
        {
            bool duplicate = await _context.ParkingSlots.AnyAsync(s => s.SlotNumber == slot.SlotNumber);
            if (duplicate)
                ModelState.AddModelError(nameof(ParkingSlot.SlotNumber), "A slot with this number already exists.");

            if (!ModelState.IsValid)
                return View(slot);

            _context.ParkingSlots.Add(slot);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Parking slot added.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /ParkingSlot/Edit/5
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id)
        {
            var slot = await _context.ParkingSlots.FindAsync(id);
            if (slot == null) return NotFound();
            return View(slot);
        }

        // POST: /ParkingSlot/Edit/5  (AR-06)
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ParkingSlot slot)
        {
            var existing = await _context.ParkingSlots.FindAsync(id);
            if (existing == null) return NotFound();

            bool duplicate = await _context.ParkingSlots.AnyAsync(s => s.SlotNumber == slot.SlotNumber && s.SlotID != id);
            if (duplicate)
                ModelState.AddModelError(nameof(ParkingSlot.SlotNumber), "A slot with this number already exists.");

            if (!ModelState.IsValid)
                return View(slot);

            existing.SlotNumber = slot.SlotNumber;
            existing.Floor = slot.Floor;
            existing.VehicleType = slot.VehicleType;
            existing.Status = slot.Status;
            existing.HourlyRate = slot.HourlyRate;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Parking slot updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /ParkingSlot/Delete/5
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var slot = await _context.ParkingSlots.FindAsync(id);
            if (slot == null) return NotFound();
            return View(slot);
        }

        // POST: /ParkingSlot/Delete/5  (AR-07)
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = Roles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var slot = await _context.ParkingSlots.FindAsync(id);
            if (slot == null) return NotFound();

            bool hasActiveBooking = await _context.Bookings.AnyAsync(b => b.SlotID == id &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            if (hasActiveBooking)
            {
                TempData["ErrorMessage"] = "Cannot delete a slot that has active bookings.";
                return RedirectToAction(nameof(Index));
            }

            _context.ParkingSlots.Remove(slot);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Parking slot deleted.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /ParkingSlot/ChangeStatus/5  (admin quick status change)
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, SlotStatus status)
        {
            var slot = await _context.ParkingSlots.FindAsync(id);
            if (slot == null) return NotFound();

            slot.Status = status;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Slot {slot.SlotNumber} status changed to {status}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
