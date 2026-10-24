using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public enum AnnouncementCategory
    {
        General = 0,
        Maintenance = 1,
        Events = 2,
        Security = 3,
        Emergency = 4
    }

    public enum AnnouncementPriority
    {
        Normal = 0,
        Important = 1,
        Urgent = 2
    }

    public enum AnnouncementState
    {
        Draft = 0,
        Published = 1,
        Archived = 2
    }

    public class Announcement
    {
        public int Id { get; set; }

        [Required, MaxLength(140)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(4000)]
        public string Content { get; set; } = string.Empty;

        public AnnouncementCategory Category { get; set; } = AnnouncementCategory.General;

        public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

        [ConcurrencyCheck]
        public AnnouncementState State { get; set; } = AnnouncementState.Draft;

        public bool IsPinned { get; set; } = false;

        [Required]
        public string CreatedByAdminId { get; set; } = string.Empty;
        public virtual ApplicationUser CreatedByAdmin { get; set; } = null!;

        public string? LastEditedByAdminId { get; set; }
        public virtual ApplicationUser? LastEditedByAdmin { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        public virtual ICollection<AnnouncementRead> Reads { get; set; } = new List<AnnouncementRead>();
    }

    public class AnnouncementRead
    {
        public int Id { get; set; }

        public int AnnouncementId { get; set; }
        public virtual Announcement Announcement { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        public DateTime FirstReadAtUtc { get; set; } = DateTime.UtcNow;
    }
}