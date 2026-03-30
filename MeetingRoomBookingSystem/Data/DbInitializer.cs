using Microsoft.AspNetCore.Identity;
using MeetingRoomBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBookingSystem.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider, AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Apply migrations (creates database if it doesn't exist and applies pending migrations)
            await context.Database.MigrateAsync();

            // Create roles
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Create admin user
            var adminUser = await userManager.FindByEmailAsync("admin@mrbs.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@mrbs.com",
                    Email = "admin@mrbs.com",
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(adminUser, "Admin@123");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // Seed rooms
            if (!context.Rooms.Any())
            {
                context.Rooms.AddRange(
                    new Room { RoomName = "Boardroom", Capacity = 20, Location = "Floor 1" },
                    new Room { RoomName = "Conference A", Capacity = 10, Location = "Floor 2" },
                    new Room { RoomName = "Conference B", Capacity = 8, Location = "Floor 2" },
                    new Room { RoomName = "Training Room", Capacity = 30, Location = "Floor 3" }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
