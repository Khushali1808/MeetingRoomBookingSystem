using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingRoomBookingSystem.Data;
using MeetingRoomBookingSystem.Models;

namespace MeetingRoomBookingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalRooms = await _context.Rooms.CountAsync();
            ViewBag.TotalBookings = await _context.Bookings.CountAsync();
            ViewBag.PendingCount = await _context.Bookings.CountAsync(b => b.Status == "Pending");

            // Chart data: bookings per room
            var bookingsPerRoom = await _context.Bookings
                .Include(b => b.Room)
                .GroupBy(b => b.Room!.RoomName)
                .Select(g => new { Room = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(bookingsPerRoom.Select(x => x.Room));
            ViewBag.ChartData = System.Text.Json.JsonSerializer.Serialize(bookingsPerRoom.Select(x => x.Count));

            // Status distribution
            var statusDist = await _context.Bookings
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.StatusLabels = System.Text.Json.JsonSerializer.Serialize(statusDist.Select(x => x.Status));
            ViewBag.StatusData = System.Text.Json.JsonSerializer.Serialize(statusDist.Select(x => x.Count));

            var bookings = await _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .OrderByDescending(b => b.Date)
                .ThenBy(b => b.StartTime)
                .ToListAsync();

            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null)
            {
                booking.Status = "Approved";
                _context.Update(booking);

                // Audit Log
                _context.AuditLogs.Add(new AuditLog {
                    UserId = _userManager.GetUserId(User) ?? string.Empty,
                    Action = "Approved Booking",
                    Details = $"Approved Booking {id}"
                });

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null)
            {
                booking.Status = "Rejected";

                // Audit Log
                _context.AuditLogs.Add(new AuditLog {
                    UserId = _userManager.GetUserId(User) ?? string.Empty,
                    Action = "Rejected Booking",
                    Details = $"Rejected Booking {id}"
                });

                _context.Update(booking);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> AuditLogs()
        {
            var logs = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(100)
                .ToListAsync();
            return View(logs);
        }
    }
}
