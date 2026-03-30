using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MeetingRoomBookingSystem.Data;
using MeetingRoomBookingSystem.Models;

namespace MeetingRoomBookingSystem.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BookingController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> MyBookings()
        {
            var userId = _userManager.GetUserId(User);
            var appDbContext = _context.Bookings.Include(b => b.Room).Where(b => b.UserId == userId).OrderByDescending(b => b.Date).ThenBy(b => b.StartTime);
            return View(await appDbContext.ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> CalendarData()
        {
            var userId = _userManager.GetUserId(User);
            var bookings = await _context.Bookings
                .Include(b => b.Room)
                .Where(b => b.UserId == userId)
                .Select(b => new
                {
                    id = b.BookingId,
                    title = b.Room.RoomName,
                    start = b.Date.ToString("yyyy-MM-dd") + "T" + b.StartTime.ToString(@"hh\:mm\:ss"),
                    end = b.Date.ToString("yyyy-MM-dd") + "T" + b.EndTime.ToString(@"hh\:mm\:ss"),
                    color = b.Status == "Approved" ? "#28a745" : (b.Status == "Rejected" ? "#dc3545" : "#ffc107")
                })
                .ToListAsync();

            return Json(bookings);
        }

        public async Task<IActionResult> Rebook(int id)
        {
            var oldBooking = await _context.Bookings.FindAsync(id);
            if (oldBooking == null || oldBooking.UserId != _userManager.GetUserId(User)) 
                return NotFound();

            return RedirectToAction(nameof(Create), new { roomId = oldBooking.RoomId });
        }

        public IActionResult Create(int? roomId)
        {
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", roomId);
            
            var model = new Booking 
            { 
                Date = DateTime.Today,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 0, 0)
            };
            
            if (roomId.HasValue)
            {
                model.RoomId = roomId.Value;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoomId,Date,StartTime,EndTime")] Booking booking)
        {
            booking.UserId = _userManager.GetUserId(User) ?? string.Empty;

            if (booking.StartTime >= booking.EndTime)
            {
                ModelState.AddModelError("EndTime", "End time must be after start time.");
            }
            else
            {
                // Policy 1: No past dates
                if (booking.Date < DateTime.Today)
                {
                    ModelState.AddModelError("Date", "You cannot book a room in the past.");
                }

                // Policy 2: Max 14 days in advance
                if (booking.Date > DateTime.Today.AddDays(14))
                {
                    ModelState.AddModelError("Date", "You can only book up to 14 days in advance.");
                }

                // Policy 3: Time logic for today
                if (booking.Date == DateTime.Today && booking.StartTime < DateTime.Now.TimeOfDay)
                {
                     ModelState.AddModelError("StartTime", "Start time cannot be in the past for today's bookings.");
                }

                // Double booking logic check!
                bool isRoomBooked = await _context.Bookings.AnyAsync(b =>
                    b.RoomId == booking.RoomId &&
                    b.Date == booking.Date &&
                    b.Status != "Rejected" && // Exclude rejected from conflicts
                    b.StartTime < booking.EndTime &&
                    b.EndTime > booking.StartTime);

                if (isRoomBooked)
                {
                    ModelState.AddModelError(string.Empty, "Room already booked for this time slot.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(booking);
                
                // Audit Log
                _context.AuditLogs.Add(new AuditLog {
                    UserId = booking.UserId,
                    Action = "Created Booking",
                    Details = $"Room {booking.RoomId} on {booking.Date.ToShortDateString()}"
                });

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(MyBookings));
            }
            
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", booking.RoomId);
            return View(booking);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null || booking.UserId != _userManager.GetUserId(User))
            {
                return NotFound();
            }

            var bookingStart = booking.Date.Add(booking.StartTime);
            if ((bookingStart - DateTime.Now).TotalHours < 2)
            {
                TempData["Error"] = "Cannot reschedule within 2 hours of the meeting or if it has already passed.";
                return RedirectToAction(nameof(MyBookings));
            }
            
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", booking.RoomId);
            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BookingId,RoomId,Date,StartTime,EndTime")] Booking booking)
        {
            if (id != booking.BookingId) return NotFound();

            var existingBooking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.BookingId == id);
            if (existingBooking == null || existingBooking.UserId != _userManager.GetUserId(User))
            {
                return NotFound();
            }

            var bookingStartCheck = existingBooking.Date.Add(existingBooking.StartTime);
            if ((bookingStartCheck - DateTime.Now).TotalHours < 2)
            {
                TempData["Error"] = "Cannot reschedule within 2 hours of the meeting or if it has already passed.";
                return RedirectToAction(nameof(MyBookings));
            }

            booking.UserId = existingBooking.UserId; // Keep same user

            if (booking.StartTime >= booking.EndTime)
            {
                ModelState.AddModelError("EndTime", "End time must be after start time.");
            }
            else
            {
                // Policy 1: No past dates
                if (booking.Date < DateTime.Today)
                {
                    ModelState.AddModelError("Date", "You cannot book a room in the past.");
                }

                // Policy 2: Max 14 days in advance
                if (booking.Date > DateTime.Today.AddDays(14))
                {
                    ModelState.AddModelError("Date", "You can only book up to 14 days in advance.");
                }

                // Policy 3: Time logic for today
                if (booking.Date == DateTime.Today && booking.StartTime < DateTime.Now.TimeOfDay)
                {
                     ModelState.AddModelError("StartTime", "Start time cannot be in the past for today's bookings.");
                }

                // Check conflict excluding current booking
                bool isRoomBooked = await _context.Bookings.AnyAsync(b =>
                    b.BookingId != booking.BookingId &&
                    b.RoomId == booking.RoomId &&
                    b.Date == booking.Date &&
                    b.Status != "Rejected" &&
                    b.StartTime < booking.EndTime &&
                    b.EndTime > booking.StartTime);

                if (isRoomBooked)
                {
                    ModelState.AddModelError(string.Empty, "Room already booked for this time slot.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(booking);

                    // Audit Log
                    _context.AuditLogs.Add(new AuditLog {
                        UserId = booking.UserId,
                        Action = "Edited Booking",
                        Details = $"Rescheduled Booking {booking.BookingId} for Room {booking.RoomId} to {booking.Date.ToShortDateString()}"
                    });

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookingExists(booking.BookingId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(MyBookings));
            }
            
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", booking.RoomId);
            return View(booking);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null && booking.UserId == _userManager.GetUserId(User))
            {
                // Cancellation Policy
                var bookingStart = booking.Date.Add(booking.StartTime);
                if ((bookingStart - DateTime.Now).TotalHours < 2)
                {
                    TempData["Error"] = "Cannot cancel within 2 hours of the meeting or if it has already passed.";
                    return RedirectToAction(nameof(MyBookings));
                }

                _context.Bookings.Remove(booking);

                // Audit Log
                _context.AuditLogs.Add(new AuditLog {
                    UserId = booking.UserId,
                    Action = "Cancelled Booking",
                    Details = $"Booking {booking.BookingId} for Room {booking.RoomId}"
                });

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(MyBookings));
        }

        [HttpGet]
        public async Task<IActionResult> CheckIn(int id)
        {
            var booking = await _context.Bookings.Include(b => b.Room).FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (booking.UserId != userId)
                return Forbid();

            if (booking.IsCheckedIn)
            {
                TempData["Info"] = "You have already checked in for this booking.";
                return RedirectToAction(nameof(MyBookings));
            }

            if (booking.Status != "Approved")
            {
                TempData["Error"] = "Only approved bookings can be checked in.";
                return RedirectToAction(nameof(MyBookings));
            }

            // Check if within 15 minutes of start time
            var bookingStart = booking.Date.Add(booking.StartTime);
            var now = DateTime.Now;
            var minutesUntilStart = (bookingStart - now).TotalMinutes;

            if (minutesUntilStart > 15)
            {
                TempData["Error"] = $"Check-in opens 15 minutes before your booking starts ({bookingStart:hh\\:mm tt}).";
                return RedirectToAction(nameof(MyBookings));
            }

            var bookingEnd = booking.Date.Add(booking.EndTime);
            if (now > bookingEnd)
            {
                TempData["Error"] = "This booking has already ended.";
                return RedirectToAction(nameof(MyBookings));
            }

            booking.IsCheckedIn = true;
            _context.Update(booking);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId ?? string.Empty,
                Action = "Checked In",
                Details = $"Checked in to Booking {booking.BookingId} for {booking.Room?.RoomName}"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"✅ Successfully checked in to {booking.Room?.RoomName}!";
            return RedirectToAction(nameof(MyBookings));
        }

        [HttpGet]
        public async Task<IActionResult> GetQrCode(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null || booking.UserId != _userManager.GetUserId(User))
                return NotFound();

            var checkInUrl = Url.Action("CheckIn", "Booking", new { id = booking.BookingId }, Request.Scheme);

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(checkInUrl ?? "", QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(10);

            return File(qrCodeBytes, "image/png");
        }

        private bool BookingExists(int id)
        {
            return _context.Bookings.Any(e => e.BookingId == id);
        }
    }
}
