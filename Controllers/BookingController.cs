using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;

namespace SmartParkingMS.Controllers
{
    // Implements Section 11 (Parking Slot Booking) and Section 12 (Booking Management)
    [Authorize]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BookingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Booking  (Customer: view current bookings; Admin: view all bookings)
        public async Task<IActionResult> Index(string? status, string? search)
        {
            IQueryable<Booking> query = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Vehicle)
                .Include(b => b.ParkingSlot);

            if (User.IsInRole(Roles.Customer))
            {
                var userId = _userManager.GetUserId(User);
                query = query.Where(b => b.UserID == userId);
            }
            else if (User.IsInRole(Roles.Admin))
            {
                // AR-09/search bookings by user, slot, or booking id
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(b =>
                        b.BookingID.ToString() == search ||
                        (b.User != null && b.User.FullName.Contains(search)) ||
                        (b.ParkingSlot != null && b.ParkingSlot.SlotNumber.Contains(search)));
                }
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, out var parsedStatus))
                query = query.Where(b => b.Status == parsedStatus);

            var bookings = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
            ViewBag.StatusFilter = status;
            ViewBag.Search = search;
            return View(bookings);
        }

        // GET: /Booking/Create?slotId=5  (Customer initiates a reservation)
        [Authorize(Roles = Roles.Customer)]
        public async Task<IActionResult> Create(int? slotId)
        {
            var userId = _userManager.GetUserId(User);

            var vehicles = await _context.Vehicles.Where(v => v.UserID == userId).ToListAsync();
            if (!vehicles.Any())
            {
                TempData["ErrorMessage"] = "You must register a vehicle before booking a parking slot.";
                return RedirectToAction("Create", "Vehicle");
            }

            var availableSlots = await _context.ParkingSlots
                .Where(s => s.Status == SlotStatus.Available)
                .ToListAsync();

            ViewBag.Vehicles = vehicles;
            ViewBag.Slots = availableSlots;

            var booking = new Booking
            {
                BookingDate = DateTime.Today,
                ExpectedEntryTime = DateTime.Now.AddMinutes(30),
                ExpectedExitTime = DateTime.Now.AddHours(2)
            };
            if (slotId.HasValue) booking.SlotID = slotId.Value;

            return View(booking);
        }

        // POST: /Booking/Create
        [HttpPost]
        [Authorize(Roles = Roles.Customer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Booking booking)
        {
            var userId = _userManager.GetUserId(User)!;

            ModelState.Remove(nameof(Booking.UserID));
            ModelState.Remove(nameof(Booking.User));
            ModelState.Remove(nameof(Booking.Status));

            // Business rule: vehicle must belong to the current user
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleID == booking.VehicleID && v.UserID == userId);
            if (vehicle == null)
                ModelState.AddModelError(nameof(Booking.VehicleID), "Please select a valid vehicle you own.");

            var slot = await _context.ParkingSlots.FindAsync(booking.SlotID);
            if (slot == null)
                ModelState.AddModelError(nameof(Booking.SlotID), "Please select a valid parking slot.");
            else if (slot.Status != SlotStatus.Available)
                ModelState.AddModelError(nameof(Booking.SlotID), "This slot is no longer available.");

            if (booking.ExpectedExitTime <= booking.ExpectedEntryTime)
                ModelState.AddModelError(nameof(Booking.ExpectedExitTime), "Exit time must be after entry time.");

            // Business rule: a slot cannot be booked by multiple users for the same time period.
            bool overlapping = await _context.Bookings.AnyAsync(b =>
                b.SlotID == booking.SlotID &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                b.ExpectedEntryTime < booking.ExpectedExitTime &&
                booking.ExpectedEntryTime < b.ExpectedExitTime);
            if (overlapping)
                ModelState.AddModelError(string.Empty, "This slot is already booked for an overlapping time period.");

            if (!ModelState.IsValid)
            {
                ViewBag.Vehicles = await _context.Vehicles.Where(v => v.UserID == userId).ToListAsync();
                ViewBag.Slots = await _context.ParkingSlots.Where(s => s.Status == SlotStatus.Available).ToListAsync();
                return View(booking);
            }

            booking.UserID = userId;
            booking.Status = BookingStatus.Confirmed;
            booking.CreatedAt = DateTime.Now;

            _context.Bookings.Add(booking);

            // Reflect reservation on the slot
            slot!.Status = SlotStatus.Reserved;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Booking confirmed. Your Booking ID is {booking.BookingID}.";
            return RedirectToAction(nameof(Details), new { id = booking.BookingID });
        }

        // GET: /Booking/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Vehicle)
                .Include(b => b.ParkingSlot)
                .Include(b => b.ParkingSession)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null) return NotFound();

            // Business rule: customers can only view their own bookings
            if (User.IsInRole(Roles.Customer) && booking.UserID != _userManager.GetUserId(User))
                return Forbid();

            return View(booking);
        }

        // GET: /Booking/Cancel/5
        [Authorize(Roles = Roles.Customer)]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = _userManager.GetUserId(User);
            var booking = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null) return NotFound();
            return View(booking);
        }

        // POST: /Booking/Cancel/5  (Business rule: a booking can be cancelled only before check-in)
        [HttpPost, ActionName("Cancel")]
        [Authorize(Roles = Roles.Customer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var booking = await _context.Bookings
                .Include(b => b.ParkingSlot)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.UserID == userId);

            if (booking == null) return NotFound();

            if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
            {
                TempData["ErrorMessage"] = "This booking cannot be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            // Confirmed reservations are chargeable even if cancelled before check-in.
            // Pending reservations can be cancelled without a charge.
            if (booking.Status == BookingStatus.Pending)
            {
                booking.Status = BookingStatus.Cancelled;
                if (booking.ParkingSlot != null && booking.ParkingSlot.Status == SlotStatus.Reserved)
                    booking.ParkingSlot.Status = SlotStatus.Available;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Pending booking cancelled without charge.";
                return RedirectToAction(nameof(Index));
            }

            var expectedDuration = booking.ExpectedExitTime - booking.ExpectedEntryTime;
            var chargeableHours = Math.Max(1, Math.Ceiling(expectedDuration.TotalHours));
            var rate = booking.ParkingSlot?.HourlyRate ?? 0;
            var cancellationFee = (decimal)chargeableHours * rate;

            var payment = booking.Payment;
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = booking.BookingID,
                    UserID = booking.UserID,
                    Amount = cancellationFee,
                    PaymentMethod = PaymentMethod.Offline,
                    PaymentStatus = PaymentStatus.Pending,
                    PaymentDate = DateTime.Now
                };
                _context.Payments.Add(payment);
            }
            else
            {
                payment.Amount = cancellationFee;
                payment.PaymentStatus = PaymentStatus.Pending;
                payment.PaymentMethod = PaymentMethod.Offline;
                payment.PaymentDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"This confirmed reservation has a cancellation charge of {cancellationFee:0.00} BDT. Pay to release the reserved slot.";
            return RedirectToAction("Pay", "Payment", new { id = payment.PaymentID });
        }

        // POST: /Booking/UpdateStatus/5  (AR-10: Admin updates booking status when necessary)
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, BookingStatus status)
        {
            var booking = await _context.Bookings.Include(b => b.ParkingSlot).FirstOrDefaultAsync(b => b.BookingID == id);
            if (booking == null) return NotFound();

            booking.Status = status;
            if (status == BookingStatus.Cancelled && booking.ParkingSlot != null && booking.ParkingSlot.Status == SlotStatus.Reserved)
                booking.ParkingSlot.Status = SlotStatus.Available;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Booking status updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
