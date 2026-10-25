using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class DailyHelpDirectoryViewModel
    {
        public DailyHelpCategory? SelectedCategory { get; set; }
        public string? SearchQuery { get; set; }
        public bool SavedOnly { get; set; }

        public int SavedCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalListings { get; set; }

        public List<DailyHelpCardViewModel> Listings { get; set; } = new();
    }

    public class DailyHelpCardViewModel
    {
        public int Id { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public DailyHelpCategory Category { get; set; }
        public string CategoryDisplay { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string? ServiceArea { get; set; }
        public string? AvailabilityNotes { get; set; }
        public DailyHelpVerificationStatus VerificationStatus { get; set; }
        public DailyHelpListingSource Source { get; set; }
        public bool HasContactPhone { get; set; }
        public bool IsSavedByCurrentUser { get; set; }

        // Admin-only fields (populated only in /DailyHelp/Admin)
        public DailyHelpListingStatus Status { get; set; }
        public string? AdminInternalNotes { get; set; }
        public string? RecommenderDisplay { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class DailyHelpDetailsViewModel
    {
        public int Id { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public DailyHelpCategory Category { get; set; }
        public string CategoryDisplay { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string? ServiceArea { get; set; }
        public string? AvailabilityNotes { get; set; }
        public string? ExperienceNotes { get; set; }
        public DailyHelpListingStatus Status { get; set; }
        public DailyHelpListingSource Source { get; set; }
        public DailyHelpVerificationStatus VerificationStatus { get; set; }
        public DateTime? VerifiedAtUtc { get; set; }

        public bool HasContactPhone { get; set; }
        public bool IsContactRevealed { get; set; }
        public string? RevealedContactPhone { get; set; }
        public bool IsSavedByCurrentUser { get; set; }

        // Admin-only context
        public bool IsAdmin { get; set; }
        public string? AdminInternalNotes { get; set; }
        public string? VerifiedByAdminName { get; set; }
        public string? RecommenderInternalDisplay { get; set; }
    }

    public class ResidentRecommendDailyHelpViewModel
    {
        [Required(ErrorMessage = "Please enter the service provider's name.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        [Display(Name = "Provider Full Name")]
        public string ProviderName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Service Category")]
        public DailyHelpCategory Category { get; set; } = DailyHelpCategory.MaidHousekeeping;

        [Required(ErrorMessage = "Please describe the services offered.")]
        [StringLength(600, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 600 characters.")]
        [Display(Name = "Services Offered & Recommendation Note")]
        public string ShortDescription { get; set; } = string.Empty;

        [StringLength(120)]
        [Display(Name = "Towers / Area Served (Optional)")]
        public string? ServiceArea { get; set; }

        [StringLength(150)]
        [Display(Name = "Typical Availability / Timings (Optional)")]
        public string? AvailabilityNotes { get; set; }

        [StringLength(120)]
        [Display(Name = "Known Experience in Society (Optional)")]
        public string? ExperienceNotes { get; set; }

        [Required(ErrorMessage = "Please provide a contact phone number for society office review.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(25, MinimumLength = 8)]
        [Display(Name = "Provider Contact Phone")]
        public string ContactPhone { get; set; } = string.Empty;
    }

    public class AdminDailyHelpFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Provider name is required.")]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Provider Full Name")]
        public string ProviderName { get; set; } = string.Empty;

        [Required]
        public DailyHelpCategory Category { get; set; } = DailyHelpCategory.MaidHousekeeping;

        [Required(ErrorMessage = "Short description is required.")]
        [StringLength(600, MinimumLength = 10)]
        [Display(Name = "Service Summary")]
        public string ShortDescription { get; set; } = string.Empty;

        [StringLength(120)]
        [Display(Name = "Service Area (e.g. All Towers A–D)")]
        public string? ServiceArea { get; set; }

        [StringLength(150)]
        [Display(Name = "Availability Schedule / Notes")]
        public string? AvailabilityNotes { get; set; }

        [StringLength(120)]
        [Display(Name = "Experience Notes (Optional)")]
        public string? ExperienceNotes { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(25)]
        [Display(Name = "Contact Phone Number")]
        public string? ContactPhone { get; set; }

        [Required]
        [Display(Name = "Listing Status")]
        public DailyHelpListingStatus Status { get; set; } = DailyHelpListingStatus.Active;

        [Required]
        [Display(Name = "Listing Verification Label")]
        public DailyHelpVerificationStatus VerificationStatus { get; set; } = DailyHelpVerificationStatus.SocietyListed;

        [StringLength(1000)]
        [Display(Name = "Internal Administrator Notes (Hidden from Residents)")]
        public string? AdminInternalNotes { get; set; }
    }

    public class AdminDailyHelpIndexViewModel
    {
        public DailyHelpListingStatus? SelectedStatus { get; set; }
        public DailyHelpCategory? SelectedCategory { get; set; }
        public string? SearchQuery { get; set; }

        public int ActiveCount { get; set; }
        public int PendingCount { get; set; }
        public int InactiveCount { get; set; }

        public List<DailyHelpCardViewModel> Listings { get; set; } = new();
    }
}