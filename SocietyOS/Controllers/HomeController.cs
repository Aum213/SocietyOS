using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocietyOS.Data;
using SocietyOS.Models;
using SocietyOS.Services;

namespace SocietyOS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IDailyMaximService _maximService;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            IDailyMaximService? maximService = null)
        {
            _logger = logger;
            _context = context;
            _maximService = maximService ?? new DailyMaximService();
        }

        public async Task<IActionResult> Index()
        {
            var isAuth = User.Identity?.IsAuthenticated ?? false;
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var userEmail = User.Identity?.Name ?? "resident@societyos.local";
            var displayName = isAuth
                ? (userEmail.Contains('@') ? userEmail.Split('@')[0] : userEmail)
                : "Aarav Sharma";

            var flatNumber = "B-204";
            var buildingName = "Tower B (Gulmohar)";
            var roleName = isAuth ? "Resident" : "Resident (Demo)";

            if (isAuth && !string.IsNullOrEmpty(currentUserId))
            {
                var userRecord = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == currentUserId);

                if (userRecord != null && !string.IsNullOrWhiteSpace(userRecord.FullName))
                {
                    displayName = userRecord.FullName;
                }

                var residentProfile = await _context.ResidentProfiles
                    .AsNoTracking()
                    .Include(r => r.Flat)
                        .ThenInclude(f => f.Building)
                    .FirstOrDefaultAsync(r => r.UserId == currentUserId && r.IsVerified);

                if (residentProfile != null)
                {
                    flatNumber = $"{residentProfile.Flat.Building.Code}-{residentProfile.Flat.FlatNumber}";
                    buildingName = residentProfile.Flat.Building.Name;
                    roleName = $"Resident ({residentProfile.ResidentType})";
                }
                else if (User.IsInRole("Administrator"))
                {
                    flatNumber = "ADMIN";
                    buildingName = "Society Management Office";
                    roleName = "Administrator";
                }
                else if (User.IsInRole("SecurityGuard"))
                {
                    flatNumber = "GATE-1";
                    buildingName = "Security Desk";
                    roleName = "Security Guard";
                }
            }

            // Query live published, unexpired announcements from database
            var nowUtc = DateTime.UtcNow;
            var eligibleNoticesQuery = _context.Announcements
                .AsNoTracking()
                .Where(a => a.State == AnnouncementState.Published &&
                            (!a.ExpiresAtUtc.HasValue || a.ExpiresAtUtc.Value > nowUtc));

            var unreadNoticeCount = isAuth
                ? await eligibleNoticesQuery.CountAsync(a => !a.Reads.Any(r => r.UserId == currentUserId))
                : 0;

            var latestNotices = await eligibleNoticesQuery
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.Priority)
                .ThenByDescending(a => a.PublishedAtUtc ?? a.CreatedAtUtc)
                .Take(3)
                .Select(a => new CommunityUpdatePreview
                {
                    Title = a.Title,
                    Category = a.Category.ToString(),
                    TimeAgo = (a.PublishedAtUtc ?? a.CreatedAtUtc).ToString("dd MMM"),
                    IsUrgent = a.Priority == AnnouncementPriority.Urgent,
                    Badge = a.Priority == AnnouncementPriority.Urgent
                        ? "Urgent Alert"
                        : (a.IsPinned ? "Pinned Notice" : a.Category.ToString())
                })
                .ToListAsync();

            var model = new HomeViewModel
            {
                IsAuthenticated = isAuth,
                ResidentName = displayName,
                Email = userEmail,
                FlatNumber = flatNumber,
                BuildingName = buildingName,
                RoleName = roleName,
                PhoneNumber = "+91 98765 43210",
                DailyMaxim = _maximService.GetTodaysMaxim(),

                // 6 Core Quick Actions (Features A, B, and C are all active!)
                PrimaryActions = new List<QuickActionItem>
                {
                    new QuickActionItem
                    {
                        Id = "sos",
                        Title = "Emergency SOS",
                        Subtitle = "Guard & Society Alert",
                        BadgeText = "Safety",
                        ActionCategory = "SOS",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Emergency SOS will alert on-duty guards and designated emergency contacts."
                    },
                    new QuickActionItem
                    {
                        Id = "daily-help",
                        Title = "Daily Help",
                        Subtitle = "Maid, Cook & Driver",
                        BadgeText = "Staff",
                        ActionCategory = "Community",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Staff entry records and attendance tracking are planned for future updates."
                    },
                    new QuickActionItem
                    {
                        Id = "visitor-pass",
                        Title = "Visitor Pass",
                        Subtitle = "Approve & View Gate Requests",
                        BadgeText = "Active",
                        ActionCategory = "Security",
                        TargetUrl = User.IsInRole("SecurityGuard") ? "/Visitors/Register" : "/Visitors/Pending",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "report-issue",
                        Title = "Report Issue",
                        Subtitle = "Community Problems & Votes",
                        BadgeText = "Active",
                        ActionCategory = "Community",
                        TargetUrl = "/Issues",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "book-amenity",
                        Title = "Book Amenity",
                        Subtitle = "Clubhouse & Court",
                        BadgeText = "Facility",
                        ActionCategory = "Facility",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Amenity slot reservations will be introduced in a future phase."
                    },
                    new QuickActionItem
                    {
                        Id = "notices",
                        Title = "Notices & Alerts",
                        Subtitle = "Official Society Circulars",
                        BadgeText = unreadNoticeCount > 0 ? $"{unreadNoticeCount} New" : "Active",
                        ActionCategory = unreadNoticeCount > 0 ? "SOS" : "General",
                        TargetUrl = "/Announcements",
                        IsPlannedFeature = false
                    }
                },

                // Expandable supplementary actions
                ExpandedActions = new List<QuickActionItem>
                {
                    new QuickActionItem
                    {
                        Id = "pay-dues",
                        Title = "Maintenance Dues",
                        Subtitle = "October Bill: ₹3,200",
                        BadgeText = "Bill",
                        ActionCategory = "Facility",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Society maintenance ledger integration is scheduled for future billing milestones."
                    },
                    new QuickActionItem
                    {
                        Id = "society-directory",
                        Title = "Intercom & Directory",
                        Subtitle = "Society Offices & Help",
                        BadgeText = "Info",
                        ActionCategory = "General",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Verified society directory will be available once all resident profiles are populated."
                    },
                    new QuickActionItem
                    {
                        Id = "gate-pass",
                        Title = "Gate Register (Guard)",
                        Subtitle = "Log Arriving Visitor",
                        BadgeText = "Gate",
                        ActionCategory = "Security",
                        TargetUrl = "/Visitors/Register",
                        IsPlannedFeature = false
                    }
                },

                // Populated with live published announcements from DB
                RecentUpdates = latestNotices
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}