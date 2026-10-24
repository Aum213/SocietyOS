using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class AnnouncementFeedViewModel
    {
        public AnnouncementCategory? SelectedCategory { get; set; }
        public AnnouncementPriority? SelectedPriority { get; set; }

        public int UnreadCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalAnnouncements { get; set; }

        public List<AnnouncementCardViewModel> Announcements { get; set; } = new();
    }

    public class AnnouncementCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ContentSnippet { get; set; } = string.Empty;
        public AnnouncementCategory Category { get; set; }
        public AnnouncementPriority Priority { get; set; }
        public AnnouncementState State { get; set; }
        public bool IsPinned { get; set; }
        public bool IsRead { get; set; }
        public bool IsExpired { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        // Admin-only metrics
        public int ReadCount { get; set; }
    }

    public class AnnouncementDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public AnnouncementCategory Category { get; set; }
        public AnnouncementPriority Priority { get; set; }
        public AnnouncementState State { get; set; }
        public bool IsPinned { get; set; }
        public bool IsRead { get; set; }
        public DateTime? FirstReadAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        public bool IsAdmin { get; set; }
        public string? CreatedByAdminName { get; set; }
        public string? LastEditedByAdminName { get; set; }
        public int TotalResidentReads { get; set; }
    }

    public class AnnouncementFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Please enter an announcement title.")]
        [StringLength(140, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 140 characters.")]
        [Display(Name = "Announcement Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Announcement body cannot be empty.")]
        [StringLength(4000, MinimumLength = 10, ErrorMessage = "Body must be between 10 and 4000 characters.")]
        [Display(Name = "Official Notice Content")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Category")]
        public AnnouncementCategory Category { get; set; } = AnnouncementCategory.General;

        [Required]
        [Display(Name = "Priority / Severity")]
        public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

        [Display(Name = "Pin to top of resident feed")]
        public bool IsPinned { get; set; }

        [Display(Name = "Expiration Date & Time (UTC, Optional)")]
        [DataType(DataType.DateTime)]
        public DateTime? ExpiresAtUtc { get; set; }

        // "Draft" or "Publish" when creating
        public string SubmitAction { get; set; } = "Publish";

        public AnnouncementState CurrentState { get; set; } = AnnouncementState.Draft;
    }

    public class AdminAnnouncementIndexViewModel
    {
        public AnnouncementState? SelectedState { get; set; }
        public AnnouncementCategory? SelectedCategory { get; set; }
        public string? SearchQuery { get; set; }

        public int PublishedActiveCount { get; set; }
        public int DraftCount { get; set; }
        public int ArchivedCount { get; set; }

        public List<AnnouncementCardViewModel> Announcements { get; set; } = new();
    }
}