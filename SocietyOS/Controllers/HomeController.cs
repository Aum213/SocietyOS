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

            var isGuard = User.IsInRole("SecurityGuard");
            var isAdmin = User.IsInRole("Administrator");

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

                PrimaryActions = new List<QuickActionItem>
                {
                    new QuickActionItem
                    {
                        Id = "sos",
                        Title = "Emergency SOS",
                        Subtitle = isGuard ? "Responder SOS Queue" : "Guard & Helpline Alert",
                        BadgeText = "SOS",
                        ActionCategory = "SOS",
                        TargetUrl = isGuard ? "/Emergency/Queue" : "/Emergency",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "daily-help",
                        Title = "Daily Help",
                        Subtitle = "Maid, Cook & Driver",
                        BadgeText = "Active",
                        ActionCategory = "Community",
                        TargetUrl = "/DailyHelp",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "visitor-pass",
                        Title = isGuard ? "Register Visitor" : "Visitor Pass",
                        Subtitle = isGuard ? "Gate Visitor Intake" : "Approve & View Gate Requests",
                        BadgeText = "Active",
                        ActionCategory = "Security",
                        TargetUrl = "/Visitors",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "report-issue",
                        Title = "Report an Issue",
                        Subtitle = "Submit a Society Problem",
                        BadgeText = "Active",
                        ActionCategory = "Community",
                        TargetUrl = "/Issues/Create",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "book-amenity",
                        Title = "Book Amenity",
                        Subtitle = "Clubhouse, Gym & Courts",
                        BadgeText = "Active",
                        ActionCategory = "Facility",
                        TargetUrl = "/Amenities",
                        IsPlannedFeature = false
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

                ExpandedActions = new List<QuickActionItem>
                {
                    new QuickActionItem
                    {
                        Id = "pay-dues",
                        Title = "Maintenance Dues",
                        Subtitle = "Bills, Balances & Receipts",
                        BadgeText = "Active",
                        ActionCategory = "Facility",
                        TargetUrl = isAdmin ? "/Maintenance/Admin" : "/Maintenance",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "society-directory",
                        Title = "Intercom Directory",
                        Subtitle = "Gate & Office Contacts",
                        BadgeText = "Active",
                        ActionCategory = "General",
                        TargetUrl = "/Emergency",
                        IsPlannedFeature = false
                    },
                    new QuickActionItem
                    {
                        Id = "gate-pass",
                        Title = "Gate Desk",
                        Subtitle = isGuard ? "Register Arriving Visitor" : "Guard Gate Registration",
                        BadgeText = isGuard || isAdmin ? "Active" : "Guard Only",
                        ActionCategory = "Security",
                        TargetUrl = isGuard || isAdmin ? "/Visitors/Register" : "/Visitors",
                        IsPlannedFeature = false
                    }
                },

                RecentUpdates = latestNotices
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Market()
        {
            return RedirectToAction("Index", "Marketplace");
        }

        [HttpGet]
        public IActionResult More()
        {
            return View();
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