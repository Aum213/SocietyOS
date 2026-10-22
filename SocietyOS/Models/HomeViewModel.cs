using System.Collections.Generic;

namespace SocietyOS.Models
{
    public class HomeViewModel
    {
        // Resident identity
        public string ResidentName { get; set; } = "Resident";
        public string FlatNumber { get; set; } = "B-204";
        public string BuildingName { get; set; } = "Tower B (Gulmohar)";
        public string RoleName { get; set; } = "Resident";
        public string Email { get; set; } = "resident@societyos.local";
        public string PhoneNumber { get; set; } = "+91 98765 43210";
        public bool IsAuthenticated { get; set; }

        // Section B: Daily Suvichar
        public SuvicharItem DailyMaxim { get; set; } = new();

        // Section C: Quick Actions
        public List<QuickActionItem> PrimaryActions { get; set; } = new();
        public List<QuickActionItem> ExpandedActions { get; set; } = new();

        // Section E: Community Updates Preview
        public List<CommunityUpdatePreview> RecentUpdates { get; set; } = new();
    }

    public class SuvicharItem
    {
        public string HindiQuote { get; set; } = string.Empty;
        public string Transliteration { get; set; } = string.Empty;
        public string EnglishMeaning { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int DayNumber { get; set; }
    }

    public class QuickActionItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string IconSvg { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = "#";
        public string ActionCategory { get; set; } = "General"; // SOS, Security, Community, Facility
        public bool IsPlannedFeature { get; set; } = false;
        public string PlannedPhaseNotice { get; set; } = string.Empty;
    }

    public class CommunityUpdatePreview
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public bool IsUrgent { get; set; }
        public string Badge { get; set; } = string.Empty;
    }
}