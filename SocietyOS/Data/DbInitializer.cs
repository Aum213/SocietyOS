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

            // 4. Provision Demo Security Guard
            var guardEmail = "guard@societyos.local";
            var existingGuard = await userManager.FindByEmailAsync(guardEmail);
            if (existingGuard == null)
            {
                var guardUser = new ApplicationUser
                {
                    UserName = guardEmail,
                    Email = guardEmail,
                    FullName = "Officer Vikram Singh",
                    EmailConfirmed = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(guardUser, "GuardPass@2026!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(guardUser, "SecurityGuard");

                    context.SecurityGuards.Add(new SecurityGuard
                    {
                        UserId = guardUser.Id,
                        EmployeeCode = "SEC-101",
                        AssignedGate = "Main Gate 1",
                        Shift = "Day",
                        IsOnDuty = true
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 5. Provision Demo Verified Resident for Flat B-204
            var residentEmail = "resident@societyos.local";
            var residentUser = await userManager.FindByEmailAsync(residentEmail);
            if (residentUser == null)
            {
                residentUser = new ApplicationUser
                {
                    UserName = residentEmail,
                    Email = residentEmail,
                    FullName = "Aarav Sharma",
                    EmailConfirmed = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(residentUser, "ResidentPass@2026!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(residentUser, "Resident");
                }
            }

            // Ensure Flat B-204 exists in Tower B so guards can select it
            var towerB = await context.Buildings.FirstOrDefaultAsync(b => b.Code == "B");
            if (towerB != null && residentUser != null)
            {
                var flatB204 = await context.Flats.FirstOrDefaultAsync(f => f.BuildingId == towerB.Id && f.FlatNumber == "204");
                if (flatB204 == null)
                {
                    flatB204 = new Flat { BuildingId = towerB.Id, Floor = 2, FlatNumber = "204" };
                    context.Flats.Add(flatB204);
                    await context.SaveChangesAsync();
                }

                // Ensure the resident profile exists and is marked verified
                var profile = await context.ResidentProfiles.FirstOrDefaultAsync(r => r.UserId == residentUser.Id);
                if (profile == null)
                {
                    context.ResidentProfiles.Add(new ResidentProfile
                    {
                        UserId = residentUser.Id,
                        FlatId = flatB204.Id,
                        ResidentType = ResidentType.Owner,
                        IsVerified = true,
                        VerifiedAtUtc = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                }
                else if (!profile.IsVerified || profile.FlatId != flatB204.Id)
                {
                    profile.IsVerified = true;
                    profile.FlatId = flatB204.Id;
                    profile.VerifiedAtUtc = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}

