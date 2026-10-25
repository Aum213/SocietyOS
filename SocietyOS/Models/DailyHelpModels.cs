using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public enum DailyHelpCategory
    {
        MaidHousekeeping = 0,
        Cook = 1,
        Driver = 2,
        BabysitterChildcare = 3,
        ElderCare = 4,
        Gardener = 5,
        LaundryIroning = 6,
        Other = 7
    }

    public enum DailyHelpListingStatus
    {
        PendingApproval = 0,
        Active = 1,
        Inactive = 2,
        Rejected = 3
    }

    public enum DailyHelpListingSource
    {
        SocietyManaged = 0,
        ResidentRecommended = 1
    }

    // Honest verification representation: never implies a police/law-enforcement background check
    public enum DailyHelpVerificationStatus
    {
        SocietyListed = 0,
        GatePassChecked = 1
    }

    public class DailyHelpListing
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string ProviderName { get; set; } = string.Empty;

        public DailyHelpCategory Category { get; set; } = DailyHelpCategory.MaidHousekeeping;

        [Required, MaxLength(600)]
        public string ShortDescription { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? ServiceArea { get; set; } // e.g. "All Towers (A–D)" or "Towers A & B"

        [MaxLength(150)]
        public string? AvailabilityNotes { get; set; } // e.g. "Morning 7 AM – 11 AM, Evening 5 PM – 7 PM"

        [MaxLength(120)]
        public string? ExperienceNotes { get; set; } // Optional honest experience note if known

        [MaxLength(25)]
        public string? ContactPhone { get; set; } // Never included in directory feed cards

        [ConcurrencyCheck]
        public DailyHelpListingStatus Status { get; set; } = DailyHelpListingStatus.Active;

        public DailyHelpListingSource Source { get; set; } = DailyHelpListingSource.SocietyManaged;

        public DailyHelpVerificationStatus VerificationStatus { get; set; } = DailyHelpVerificationStatus.SocietyListed;

        public string? VerifiedByAdminId { get; set; }
        public virtual ApplicationUser? VerifiedByAdmin { get; set; }
        public DateTime? VerifiedAtUtc { get; set; }

        // Strictly internal administrator notes; never exposed to residents
        [MaxLength(1000)]
        public string? AdminInternalNotes { get; set; }

        // Internal audit link if recommended by a resident; never exposed to other residents
        public string? SubmittedByResidentUserId { get; set; }
        public virtual ApplicationUser? SubmittedByResidentUser { get; set; }

        public string? LastModifiedByAdminId { get; set; }
        public virtual ApplicationUser? LastModifiedByAdmin { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public virtual ICollection<DailyHelpShortlist> Shortlists { get; set; } = new List<DailyHelpShortlist>();
    }

    public class DailyHelpShortlist
    {
        public int Id { get; set; }

        public int ListingId { get; set; }
        public virtual DailyHelpListing Listing { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}