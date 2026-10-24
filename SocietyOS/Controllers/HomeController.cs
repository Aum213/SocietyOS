using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SocietyOS.Models;
using SocietyOS.Services;

namespace SocietyOS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IDailyMaximService _maximService;

        public HomeController(ILogger<HomeController> logger, IDailyMaximService? maximService = null)
        {
            _logger = logger;
            // Fallback instantiation ensures the controller functions even before Program.cs registration
            _maximService = maximService ?? new DailyMaximService();
        }

        public IActionResult Index()
        {
            var isAuth = User.Identity?.IsAuthenticated ?? false;
            var userEmail = User.Identity?.Name ?? "resident@societyos.local";
            var displayName = isAuth ? (userEmail.Contains('@') ? userEmail.Split('@')[0] : userEmail) : "Aarav Sharma";

            var model = new HomeViewModel
            {
                IsAuthenticated = isAuth,
                ResidentName = displayName,
                Email = userEmail,
                FlatNumber = "B-204",
                BuildingName = "Tower B (Gulmohar)",
                RoleName = isAuth ? "Resident (Owner)" : "Resident (Demo)",
                PhoneNumber = "+91 98765 43210",
                DailyMaxim = _maximService.GetTodaysMaxim(),

                // 6 Core Quick Actions
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
                        PlannedPhaseNotice = "Emergency SOS will alert on-duty guards and designated emergency contacts in Phase 2."
                    },
                    new QuickActionItem
                    {
                        Id = "daily-help",
                        Title = "Daily Help",
                        Subtitle = "Maid, Cook & Driver",
                        BadgeText = "Active",
                        ActionCategory = "Community",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Staff entry records and attendance tracking will connect in Phase 2."
                    },
                    new QuickActionItem
                    {
                        Id = "visitor-pass",
                        Title = "Visitor Pass",
                        Subtitle = "Approve & View Gate Requests",
                        BadgeText = "Active",
                        ActionCategory = "Security",
                        TargetUrl = "/Visitors/Pending",
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
                        PlannedPhaseNotice = "Amenity slot reservations will be introduced in the facility management phase."
                    },
                    new QuickActionItem
                    {
                        Id = "notices",
                        Title = "Notices & Alerts",
                        Subtitle = "Feature C Workflow",
                        BadgeText = "Task 04",
                        ActionCategory = "General",
                        TargetUrl = "/Announcements",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Feature C (Official Society Announcements) is scheduled for Task 04."
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
                        PlannedPhaseNotice = "Verified society directory will be available once resident profiles are populated."
                    },
                    new QuickActionItem
                    {
                        Id = "gate-pass",
                        Title = "Delivery Pass",
                        Subtitle = "Amazon, Swiggy, Zomato",
                        BadgeText = "Gate",
                        ActionCategory = "Security",
                        IsPlannedFeature = true,
                        PlannedPhaseNotice = "Quick delivery auto-approvals will connect to the Gate Guard interface."
                    }
                },

                // Restrained Community Updates Preview
                RecentUpdates = new List<CommunityUpdatePreview>
                {
                    new CommunityUpdatePreview
                    {
                        Title = "Tower B Lift Modernization scheduled for Friday, 10 AM to 1 PM.",
                        Category = "Maintenance Alert",
                        TimeAgo = "2h ago",
                        IsUrgent = true,
                        Badge = "Urgent Notice"
                    },
                    new CommunityUpdatePreview
                    {
                        Title = "Water pressure sensor installation completed for 2nd & 3rd floors.",
                        Category = "Issue Resolved",
                        TimeAgo = "Yesterday",
                        IsUrgent = false,
                        Badge = "Issue Update"
                    }
                }
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