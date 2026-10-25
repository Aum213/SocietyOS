using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public enum IssueCategory
    {
        Plumbing = 0,
        Electrical = 1,
        LiftElevator = 2,
        CleanlinessWaste = 3,
        Security = 4,
        Parking = 5,
        StructuralMaintenance = 6,
        Other = 7
    }

    public enum IssueStatus
    {
        Reported = 0,
        Verified = 1,
        InProgress = 2,
        Resolved = 3
    }

    public enum IssueLocationScope
    {
        FlatSpecific = 0,
        BuildingCommonArea = 1,
        CommunityWide = 2
    }

    public class CommunityIssue
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        public IssueCategory Category { get; set; } = IssueCategory.Other;

        [ConcurrencyCheck]
        public IssueStatus Status { get; set; } = IssueStatus.Reported;

        public IssueLocationScope LocationScope { get; set; } = IssueLocationScope.BuildingCommonArea;

        [MaxLength(150)]
        public string? LocationDetails { get; set; }

        [Required]
        public string ReporterUserId { get; set; } = string.Empty;
        public virtual ApplicationUser ReporterUser { get; set; } = null!;

        public int? BuildingId { get; set; }
        public virtual Building? Building { get; set; }

        public int? FlatId { get; set; }
        public virtual Flat? Flat { get; set; }

        [MaxLength(255)]
        public string? PhotoStorageKey { get; set; }

        [MaxLength(1000)]
        public string? ResolutionSummary { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAtUtc { get; set; }

        // Soft-delete fields preserving vote, comment, and status-history integrity
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAtUtc { get; set; }
        public string? DeletedByUserId { get; set; }

        public virtual ICollection<IssueVote> Votes { get; set; } = new List<IssueVote>();
        public virtual ICollection<IssueComment> Comments { get; set; } = new List<IssueComment>();
        public virtual ICollection<IssueStatusHistory> StatusHistory { get; set; } = new List<IssueStatusHistory>();
    }

    public class IssueVote
    {
        public int Id { get; set; }

        public int IssueId { get; set; }
        public virtual CommunityIssue Issue { get; set; } = null!;

        [Required]
        public string VoterUserId { get; set; } = string.Empty;
        public virtual ApplicationUser VoterUser { get; set; } = null!;

        [Range(-1, 1)]
        public int VoteValue { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class IssueComment
    {
        public int Id { get; set; }

        public int IssueId { get; set; }
        public virtual CommunityIssue Issue { get; set; } = null!;

        [Required]
        public string AuthorUserId { get; set; } = string.Empty;
        public virtual ApplicationUser AuthorUser { get; set; } = null!;

        [Required, MaxLength(1000)]
        public string CommentText { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class IssueStatusHistory
    {
        public int Id { get; set; }

        public int IssueId { get; set; }
        public virtual CommunityIssue Issue { get; set; } = null!;

        public IssueStatus PreviousStatus { get; set; }
        public IssueStatus NewStatus { get; set; }

        [Required]
        public string ChangedByAdminId { get; set; } = string.Empty;
        public virtual ApplicationUser ChangedByAdmin { get; set; } = null!;

        [MaxLength(1000)]
        public string? InternalManagementNote { get; set; }

        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    }
}