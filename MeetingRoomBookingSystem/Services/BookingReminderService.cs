using Microsoft.EntityFrameworkCore;
using MeetingRoomBookingSystem.Data;
using MeetingRoomBookingSystem.Models;

namespace MeetingRoomBookingSystem.Services
{
    public class BookingReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingReminderService> _logger;
        private readonly HashSet<int> _remindedBookingIds = new();

        public BookingReminderService(IServiceProvider serviceProvider, ILogger<BookingReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📅 Booking Reminder Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpcomingBookings();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Booking Reminder Service.");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }

            _logger.LogInformation("📅 Booking Reminder Service stopped.");
        }

        private async Task CheckForUpcomingBookings()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.Now;
            var today = DateTime.Today;

            // Find approved bookings starting within the next 15 minutes
            var upcomingBookings = await context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .Where(b => b.Date == today && b.Status == "Approved")
                .ToListAsync();

            foreach (var booking in upcomingBookings)
            {
                var bookingStart = booking.Date.Add(booking.StartTime);
                var minutesUntilStart = (bookingStart - now).TotalMinutes;

                // Remind for bookings starting in 10-15 minutes (checked every 60s)
                if (minutesUntilStart > 0 && minutesUntilStart <= 15 && !_remindedBookingIds.Contains(booking.BookingId))
                {
                    _remindedBookingIds.Add(booking.BookingId);

                    _logger.LogInformation(
                        "🔔 REMINDER: {UserName} ({Email}) has a booking in {Room} starting at {StartTime} (in {Minutes:F0} minutes).",
                        booking.User?.FullName ?? "Unknown",
                        booking.User?.Email ?? "N/A",
                        booking.Room?.RoomName ?? "Unknown Room",
                        bookingStart.ToString("hh:mm tt"),
                        minutesUntilStart
                    );

                    // Also log to the audit trail
                    context.AuditLogs.Add(new AuditLog
                    {
                        UserId = booking.UserId,
                        Action = "Reminder Sent",
                        Details = $"Reminder for Booking {booking.BookingId} in {booking.Room?.RoomName} at {bookingStart:hh:mm tt}"
                    });
                }
            }

            await context.SaveChangesAsync();

            // Clean up old reminded IDs (keep only today's entries to avoid memory issues)
            if (_remindedBookingIds.Count > 1000)
            {
                _remindedBookingIds.Clear();
            }
        }
    }
}
