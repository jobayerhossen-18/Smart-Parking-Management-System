using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Models;

namespace SmartParkingMS.Data
{
    // Seeds roles, a default administrator account, and a starter set of
    // parking slots / rates so the app is usable immediately after first run.
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Roles
            string[] roles = { Roles.Admin, Roles.Customer };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Create an administrator only when credentials are supplied securely.
            // Configure Admin:Email and Admin:Password with User Secrets or environment variables.
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var adminEmail = configuration["Admin:Email"];
            var adminPassword = configuration["Admin:Password"];

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FullName = "System Administrator",
                        EmailConfirmed = true,
                        CreatedAt = DateTime.Now
                    };
                    var result = await userManager.CreateAsync(adminUser, adminPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, Roles.Admin);
                    }
                    else
                    {
                        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                        throw new InvalidOperationException($"Could not create the configured administrator account: {errors}");
                    }
                }
            }

            // 3. Starter parking layout: 10 slots total.
            // Add missing slots individually so existing installations are upgraded
            // from the original 6-slot seed without deleting current bookings.
            var starterSlots = new List<ParkingSlot>
            {
                new() { SlotNumber = "A-01", Floor = "Ground Floor", VehicleType = VehicleType.Car, Status = SlotStatus.Available, HourlyRate = 50 },
                new() { SlotNumber = "A-02", Floor = "Ground Floor", VehicleType = VehicleType.Car, Status = SlotStatus.Available, HourlyRate = 50 },
                new() { SlotNumber = "A-03", Floor = "Ground Floor", VehicleType = VehicleType.SUV, Status = SlotStatus.Available, HourlyRate = 70 },
                new() { SlotNumber = "B-01", Floor = "1st Floor", VehicleType = VehicleType.Motorcycle, Status = SlotStatus.Available, HourlyRate = 20 },
                new() { SlotNumber = "B-02", Floor = "1st Floor", VehicleType = VehicleType.Motorcycle, Status = SlotStatus.Available, HourlyRate = 20 },
                new() { SlotNumber = "B-03", Floor = "1st Floor", VehicleType = VehicleType.Car, Status = SlotStatus.Maintenance, HourlyRate = 50 },
                // Four dedicated slots for CNG/auto-rickshaws, a common vehicle type in Bangladesh.
                new() { SlotNumber = "C-01", Floor = "Ground Floor", VehicleType = VehicleType.CNG, Status = SlotStatus.Available, HourlyRate = 30 },
                new() { SlotNumber = "C-02", Floor = "Ground Floor", VehicleType = VehicleType.CNG, Status = SlotStatus.Available, HourlyRate = 30 },
                new() { SlotNumber = "C-03", Floor = "Ground Floor", VehicleType = VehicleType.CNG, Status = SlotStatus.Available, HourlyRate = 30 },
                new() { SlotNumber = "C-04", Floor = "Ground Floor", VehicleType = VehicleType.CNG, Status = SlotStatus.Available, HourlyRate = 30 },
            };

            var existingSlotNumbers = await context.ParkingSlots
                .Select(s => s.SlotNumber)
                .ToListAsync();

            var missingSlots = starterSlots
                .Where(s => !existingSlotNumbers.Contains(s.SlotNumber))
                .ToList();

            if (missingSlots.Count > 0)
                context.ParkingSlots.AddRange(missingSlots);

            // 4. Default parking rates
            var existingRateTypes = await context.ParkingRates
                .Select(r => r.VehicleType)
                .ToListAsync();

            var starterRates = new List<ParkingRate>
            {
                new ParkingRate { VehicleType = VehicleType.Car, HourlyRate = 50, MinimumChargeHours = 1 },
                new ParkingRate { VehicleType = VehicleType.Motorcycle, HourlyRate = 20, MinimumChargeHours = 1 },
                new ParkingRate { VehicleType = VehicleType.SUV, HourlyRate = 70, MinimumChargeHours = 1 },
                new ParkingRate { VehicleType = VehicleType.Other, HourlyRate = 40, MinimumChargeHours = 1 },
                new ParkingRate { VehicleType = VehicleType.CNG, HourlyRate = 30, MinimumChargeHours = 1 }
            };

            var missingRates = starterRates
                .Where(r => !existingRateTypes.Contains(r.VehicleType))
                .ToList();

            if (missingRates.Count > 0)
                context.ParkingRates.AddRange(missingRates);

            await context.SaveChangesAsync();
        }
    }
}
