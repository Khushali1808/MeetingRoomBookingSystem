using Microsoft.AspNetCore.Mvc;
using MeetingRoomBookingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBookingSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var rooms = await _context.Rooms.ToListAsync();
            return View(rooms);
        }
    }
}
