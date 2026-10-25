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

            // 2. Seed Society Topology (4 Buildings, 120+ flats)
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
                                FlatNumber = $"{floor}0{unit}"
                            });
                        }
                    }
                }
                await context.SaveChangesAsync();
            }

            // 3. Provision Initial Administrator
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
                    PhoneNumber = "+91 98765 43210",
                    EmailConfirmed = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(residentUser, "ResidentPass@2026!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(residentUser, "Resident");
                }
            }

            var towerB = await context.Buildings.FirstOrDefaultAsync(b => b.Code == "B");
            Flat? flatB204 = null;
            if (towerB != null && residentUser != null)
            {
                flatB204 = await context.Flats.FirstOrDefaultAsync(f => f.BuildingId == towerB.Id && f.FlatNumber == "204");
                if (flatB204 == null)
                {
                    flatB204 = new Flat { BuildingId = towerB.Id, Floor = 2, FlatNumber = "204" };
                    context.Flats.Add(flatB204);
                    await context.SaveChangesAsync();
                }

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

            // 6. Seed Initial Published Society Announcements
            var primaryAdmin = await userManager.FindByEmailAsync("admin@societyos.local");
            if (primaryAdmin != null && !await context.Announcements.AnyAsync())
            {
                var now = DateTime.UtcNow;
                context.Announcements.AddRange(
                    new Announcement
                    {
                        Title = "Tower B Elevator Modernization: Friday 10 AM – 1 PM",
                        Content = "Preventive maintenance and rope tension inspection will be carried out on the Tower B passenger lift this Friday between 10:00 AM and 1:00 PM. The service lift will remain operational throughout.",
                        Category = AnnouncementCategory.Maintenance,
                        Priority = AnnouncementPriority.Urgent,
                        State = AnnouncementState.Published,
                        IsPinned = true,
                        CreatedByAdminId = primaryAdmin.Id,
                        CreatedAtUtc = now.AddHours(-3),
                        UpdatedAtUtc = now.AddHours(-3),
                        PublishedAtUtc = now.AddHours(-3)
                    },
                    new Announcement
                    {
                        Title = "Annual Diwali Cultural Evening & Community Dinner",
                        Content = "All residents of Towers A, B, C, and D are cordially invited to the SocietyOS Diwali Cultural Evening at the Central Lawn this Sunday at 7:00 PM.",
                        Category = AnnouncementCategory.Events,
                        Priority = AnnouncementPriority.Normal,
                        State = AnnouncementState.Published,
                        IsPinned = false,
                        CreatedByAdminId = primaryAdmin.Id,
                        CreatedAtUtc = now.AddDays(-1),
                        UpdatedAtUtc = now.AddDays(-1),
                        PublishedAtUtc = now.AddDays(-1)
                    }
                );
                await context.SaveChangesAsync();
            }

            // 7. Seed Initial Society Amenities
            if (!await context.Amenities.AnyAsync())
            {
                var now = DateTime.UtcNow;
                context.Amenities.AddRange(
                    new Amenity
                    {
                        Name = "Badminton Court 1",
                        Description = "Indoor synthetic-mat badminton court. Non-marking shoes are mandatory. Exclusive slot reservation per flat.",
                        Location = "Sports Arena, Near Tower C",
                        IsActive = true,
                        OpeningTimeLocal = new TimeSpan(6, 0, 0),
                        ClosingTimeLocal = new TimeSpan(22, 0, 0),
                        MinBookingMinutes = 30,
                        MaxBookingMinutes = 120,
                        AdvanceBookingDays = 7,
                        MaxActiveBookingsPerResident = 2,
                        Capacity = 1,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new Amenity
                    {
                        Name = "Clubhouse Multipurpose Hall",
                        Description = "Air-conditioned community room suitable for small family gatherings, study groups, and resident committee meetings.",
                        Location = "Central Clubhouse, 1st Floor",
                        IsActive = true,
                        OpeningTimeLocal = new TimeSpan(9, 0, 0),
                        ClosingTimeLocal = new TimeSpan(21, 0, 0),
                        MinBookingMinutes = 60,
                        MaxBookingMinutes = 240,
                        AdvanceBookingDays = 14,
                        MaxActiveBookingsPerResident = 1,
                        Capacity = 1,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new Amenity
                    {
                        Name = "Community Fitness & Gym",
                        Description = "Cardio and strength training facility. Supports up to 10 concurrent residents per time window.",
                        Location = "Central Clubhouse, Ground Floor",
                        IsActive = true,
                        OpeningTimeLocal = new TimeSpan(5, 30, 0),
                        ClosingTimeLocal = new TimeSpan(22, 30, 0),
                        MinBookingMinutes = 30,
                        MaxBookingMinutes = 120,
                        AdvanceBookingDays = 7,
                        MaxActiveBookingsPerResident = 3,
                        Capacity = 10,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }
                );
                await context.SaveChangesAsync();
            }

            // 8. Seed Initial Daily Help Household Service Listings
            if (primaryAdmin != null && !await context.DailyHelpListings.AnyAsync())
            {
                var now = DateTime.UtcNow;
                context.DailyHelpListings.AddRange(
                    new DailyHelpListing
                    {
                        ProviderName = "Rekha Ben Parmar",
                        Category = DailyHelpCategory.MaidHousekeeping,
                        ShortDescription = "Experienced in daily sweeping, mopping, and utensil cleaning. Currently has afternoon slots open for Towers A and B.",
                        ServiceArea = "Towers A & B",
                        AvailabilityNotes = "Afternoon 1:30 PM – 5:00 PM",
                        ExperienceNotes = "Working in SocietyOS towers for 4 years",
                        ContactPhone = "+91 98250 11223",
                        Status = DailyHelpListingStatus.Active,
                        Source = DailyHelpListingSource.SocietyManaged,
                        VerificationStatus = DailyHelpVerificationStatus.GatePassChecked,
                        VerifiedByAdminId = primaryAdmin.Id,
                        VerifiedAtUtc = now,
                        AdminInternalNotes = "Active Gate Pass #GP-108 verified at Main Gate.",
                        LastModifiedByAdminId = primaryAdmin.Id,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new DailyHelpListing
                    {
                        ProviderName = "Meena Ben Joshi",
                        Category = DailyHelpCategory.Cook,
                        ShortDescription = "Specializes in Gujarati, North Indian, and wholesome home-style meals. Available for morning and evening cooking slots.",
                        ServiceArea = "All Towers (A–D)",
                        AvailabilityNotes = "Morning 8 AM – 11 AM, Evening 6 PM – 8 PM",
                        ExperienceNotes = "Cooking in 5 flats across Towers B & C",
                        ContactPhone = "+91 98250 33445",
                        Status = DailyHelpListingStatus.Active,
                        Source = DailyHelpListingSource.SocietyManaged,
                        VerificationStatus = DailyHelpVerificationStatus.GatePassChecked,
                        VerifiedByAdminId = primaryAdmin.Id,
                        VerifiedAtUtc = now,
                        AdminInternalNotes = "Active Gate Pass #GP-114 verified.",
                        LastModifiedByAdminId = primaryAdmin.Id,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new DailyHelpListing
                    {
                        ProviderName = "Kishore Bhai Solanki",
                        Category = DailyHelpCategory.LaundryIroning,
                        ShortDescription = "Steam pressing and doorstep cloth pickup/delivery across all four apartment buildings.",
                        ServiceArea = "All Towers (A–D)",
                        AvailabilityNotes = "Daily 9:00 AM – 7:00 PM (Closed Mondays)",
                        ContactPhone = "+91 98250 55667",
                        Status = DailyHelpListingStatus.Active,
                        Source = DailyHelpListingSource.SocietyManaged,
                        VerificationStatus = DailyHelpVerificationStatus.SocietyListed,
                        LastModifiedByAdminId = primaryAdmin.Id,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }
                );
                await context.SaveChangesAsync();
            }

            // 9. Seed Safe Default Society Emergency Desk Contacts
            if (primaryAdmin != null && !await context.SocietyEmergencyContacts.AnyAsync())
            {
                var now = DateTime.UtcNow;
                context.SocietyEmergencyContacts.AddRange(
                    new SocietyEmergencyContact
                    {
                        DisplayName = "Main Security Gate 1 Desk (Demo)",
                        PhoneNumber = "0281-2220101",
                        Description = "24×7 On-Duty Guard Desk at Main Entrance",
                        IsActive = true,
                        DisplayOrder = 1,
                        UpdatedByAdminId = primaryAdmin.Id,
                        UpdatedAtUtc = now
                    },
                    new SocietyEmergencyContact
                    {
                        DisplayName = "Society Management Office (Demo)",
                        PhoneNumber = "0281-2220102",
                        Description = "Facility Manager & Emergency Maintenance Desk",
                        IsActive = true,
                        DisplayOrder = 2,
                        UpdatedByAdminId = primaryAdmin.Id,
                        UpdatedAtUtc = now
                    }
                );
                await context.SaveChangesAsync();
            }

            // 10. Seed Initial Approved Marketplace Listings
            if (residentUser != null && flatB204 != null && towerB != null && primaryAdmin != null &&
            !await context.MarketplaceListings.AnyAsync(m => m.Title.Contains("Sheesham Wood Study Desk")))
            {
                var now = DateTime.UtcNow;
                context.MarketplaceListings.AddRange(
                    new MarketplaceListing
                    {
                        Title = "Solid Sheesham Wood Study Desk with Ergonomic Chair",
                        Description = "4-foot solid wood study table with drawer and mesh ergonomic chair. Gently used for 1 year, no scratches. Buyer can inspect and pick up from Tower B.",
                        Category = MarketplaceCategory.FurnitureAndHome,
                        ListingType = MarketplaceListingType.ForSale,
                        PriceInr = 4500m,
                        Condition = MarketplaceItemCondition.LikeNew,
                        Status = MarketplaceListingStatus.Active,
                        SellerUserId = residentUser.Id,
                        FlatId = flatB204.Id,
                        BuildingId = towerB.Id,
                        SellerContactPhone = "+91 98765 43210",
                        ModeratedByAdminId = primaryAdmin.Id,
                        ModeratedAtUtc = now,
                        CreatedAtUtc = now.AddHours(-6),
                        UpdatedAtUtc = now.AddHours(-6)
                    },
                    new MarketplaceListing
                    {
                        Title = "B.Tech Engineering Textbooks (Sem 1 to Sem 4 Set)",
                        Description = "Complete set of Computer Engineering reference books in clean condition. Free giveaway to any junior student in the society.",
                        Category = MarketplaceCategory.BooksAndStudy,
                        ListingType = MarketplaceListingType.FreeGiveaway,
                        PriceInr = null,
                        Condition = MarketplaceItemCondition.Good,
                        Status = MarketplaceListingStatus.Active,
                        SellerUserId = residentUser.Id,
                        FlatId = flatB204.Id,
                        BuildingId = towerB.Id,
                        SellerContactPhone = "+91 98765 43210",
                        ModeratedByAdminId = primaryAdmin.Id,
                        ModeratedAtUtc = now,
                        CreatedAtUtc = now.AddDays(-1),
                        UpdatedAtUtc = now.AddDays(-1)
                    }
                );
                await context.SaveChangesAsync();
            }
            // 11. Seed Initial Maintenance Bills for Flat B-204
            if (flatB204 != null && primaryAdmin != null && !await context.MaintenanceBills.AnyAsync())
            {
                var nowUtc = DateTime.UtcNow;
                var sepBill = new MaintenanceBill
                {
                    BillNumber = "BILL-202609-B204",
                    FlatId = flatB204.Id,
                    BillingYear = 2026,
                    BillingMonth = 9,
                    IssueDateLocal = new DateOnly(2026, 9, 1),
                    DueDateLocal = new DateOnly(2026, 9, 15),
                    Description = "Monthly Society Maintenance & Common Area Charges (September 2026)",
                    OriginalAmountInr = 3200.00m,
                    CreatedByAdminId = primaryAdmin.Id,
                    CreatedAtUtc = nowUtc.AddDays(-45),
                    UpdatedAtUtc = nowUtc.AddDays(-35)
                };

                var octBill = new MaintenanceBill
                {
                    BillNumber = "BILL-202610-B204",
                    FlatId = flatB204.Id,
                    BillingYear = 2026,
                    BillingMonth = 10,
                    IssueDateLocal = new DateOnly(2026, 10, 1),
                    DueDateLocal = new DateOnly(2026, 10, 15),
                    Description = "Monthly Society Maintenance & Common Area Charges (October 2026)",
                    OriginalAmountInr = 3200.00m,
                    CreatedByAdminId = primaryAdmin.Id,
                    CreatedAtUtc = nowUtc.AddDays(-15),
                    UpdatedAtUtc = nowUtc.AddDays(-15)
                };

                context.MaintenanceBills.AddRange(sepBill, octBill);
                await context.SaveChangesAsync();

                context.MaintenancePayments.Add(new MaintenancePayment
                {
                    BillId = sepBill.Id,
                    PaymentReference = "RCPT-20260910-1042",
                    AmountReceivedInr = 3200.00m,
                    PaymentDateLocal = new DateOnly(2026, 9, 10),
                    PaymentMethod = MaintenancePaymentMethod.UpiTransfer,
                    ExternalTransactionRef = "UTR625319004218",
                    Notes = "September maintenance settled on time.",
                    RecordedByAdminId = primaryAdmin.Id,
                    RecordedAtUtc = nowUtc.AddDays(-35)
                });
                await context.SaveChangesAsync();
            }
        }
    }
}