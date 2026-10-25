using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class CreateIssueViewModel
    {
        [Required(ErrorMessage = "Please enter a clear, concise issue title.")]
        [StringLength(120, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 120 characters.")]
        [Display(Name = "Issue Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please describe the problem in detail.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
        [Display(Name = "Detailed Description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Category")]
        public IssueCategory Category { get; set; } = IssueCategory.Plumbing;

        [Required]
        [Display(Name = "Affected Area / Scope")]
        public IssueLocationScope LocationScope { get; set; } = IssueLocationScope.BuildingCommonArea;

        [Display(Name = "Building / Tower")]
        public int? BuildingId { get; set; }

        [StringLength(150, ErrorMessage = "Location details cannot exceed 150 characters.")]
        [Display(Name = "Specific Spot / Landmark (Optional)")]
        public string? LocationDetails { get; set; }

        [Display(Name = "Attach Photo (Optional, Max 5MB)")]
        public IFormFile? Photo { get; set; }

        public string ResidentFlatDisplay { get; set; } = string.Empty;
        public List<SelectListItem> Buildings { get; set; } = new();
    }

    public class IssueFeedViewModel
    {
        public IssueStatus? SelectedStatus { get; set; }
        public IssueCategory? SelectedCategory { get; set; }
        public string SortBy { get; set; } = "most-voted"; // "most-voted" or "recent"
        public string? SearchQuery { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalIssues { get; set; }

        public bool IsAdminView { get; set; }
        public List<IssueCardViewModel> Issues { get; set; } = new();
    }

    public class IssueCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DescriptionSnippet { get; set; } = string.Empty;
        public IssueCategory Category { get; set; }
        public IssueStatus Status { get; set; }
        public string PrivacySafeLocation { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public bool HasPhoto { get; set; }

        public int NetVotes { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public int CurrentUserVote { get; set; } // 1, -1, or 0
        public int CommentCount { get; set; }

        // Populated ONLY when IsAdminView == true
        public string? AdminReporterName { get; set; }
        public string? AdminReporterFlat { get; set; }
    }

    public class IssueDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IssueCategory Category { get; set; }
        public IssueStatus Status { get; set; }
        public string PrivacySafeLocation { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        public string? ResolutionSummary { get; set; }
        public bool HasPhoto { get; set; }

        public int NetVotes { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public int CurrentUserVote { get; set; }

        public List<IssueCommentItemViewModel> Comments { get; set; } = new();

        // Admin-only context
        public bool IsAdmin { get; set; }
        public string? InternalReporterName { get; set; }
        public string? InternalReporterEmail { get; set; }
        public string? InternalReporterFlat { get; set; }
        public List<IssueStatusHistoryItemViewModel> StatusHistory { get; set; } = new();
        public bool CanDelete { get; set; }
    }

    public class IssueCommentItemViewModel
    {
        public int Id { get; set; }
        public string AuthorLabel { get; set; } = "Resident"; // Anonymous label for residents
        public bool IsAdminComment { get; set; }
        public bool IsOwnComment { get; set; }
        public string CommentText { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    public class IssueStatusHistoryItemViewModel
    {
        public IssueStatus PreviousStatus { get; set; }
        public IssueStatus NewStatus { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public string? InternalManagementNote { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class AddIssueCommentInputModel
    {
        [Required]
        public int IssueId { get; set; }

        [Required(ErrorMessage = "Comment cannot be empty.")]
        [StringLength(1000, MinimumLength = 2, ErrorMessage = "Comment must be between 2 and 1000 characters.")]
        public string CommentText { get; set; } = string.Empty;
    }

    public class VoteIssueInputModel
    {
        [Required]
        public int IssueId { get; set; }

        [Required]
        [Range(-1, 1)]
        public int VoteValue { get; set; } // 1 or -1

        public string? ReturnUrl { get; set; }
    }

    public class AdminStatusUpdateInputModel
    {
        [Required]
        public int IssueId { get; set; }

        [Required]
        public IssueStatus NewStatus { get; set; }

        [StringLength(1000, ErrorMessage = "Resolution summary cannot exceed 1000 characters.")]
        public string? ResolutionSummary { get; set; }

        [StringLength(1000, ErrorMessage = "Internal note cannot exceed 1000 characters.")]
        public string? InternalManagementNote { get; set; }
    }
}