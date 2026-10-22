using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocietyOS.Models;

namespace SocietyOS.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Ensure primary roles exist
            string[] roles = { "Administrator", "Resident", "SecurityGuard" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Society Topology (4 Buildings, 120 total flats)
            if (!await context.Buildings.AnyAsync())
            {
                var buildings = new[]
                {
                    new Building { Name = "Tower A (Amaltas)", Code = "A", TotalFloors = 10 },
                    new Building { Name = "Tower B (Gulmohar)", Code = "B", TotalFloors = 10 },
                    new Building { Name = "Tower C (Palash)", Code = "C", TotalFloors = 10 },
                    new Building { Name = "Tower D (Parijat)", Code = "D", TotalFloors = 10 }
                };

                await context.Buildings.AddRangeAsync(buildings);
                await context.SaveChangesAsync();

                foreach (var building in buildings)
                {
                    for (int floor = 1; floor <= 10; floor++)
                    {
                        for (int unit = 1; unit <= 3; unit++)
                        {
                            context.Flats.Add(new Flat
                            {
                                BuildingId = building.Id,
                                Floor = floor,
                                FlatNumber = $"{floor}0{unit}" // e.g. 101, 102, 103 ... 1003
                            });
                        }
                    }
                }
                await context.SaveChangesAsync();
            }

            // 3. Provision Initial Administrator from Configuration Allowlist
            var adminEmails = configuration.GetSection("AdminSettings:SuperAdminEmails").Get<string[]>()
                              ?? new[] { "admin@societyos.local" };

            foreach (var adminEmail in adminEmails)
            {
                var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
                if (existingAdmin == null)
                {
                    var newAdmin = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FullName = "Society Administrator",
                        EmailConfirmed = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    // Initial seed password for development
                    var result = await userManager.CreateAsync(newAdmin, "SocietyAdmin@2026!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newAdmin, "Administrator");
                    }
                }
            }
        }
    }
}