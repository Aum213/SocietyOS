using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocietyOS.Models
{
    public enum MarketplaceCategory
    {
        FurnitureAndHome = 0,
        Electronics = 1,
        BooksAndStudy = 2,
        ClothingAndAccessories = 3,
        SportsAndFitness = 4,
        KidsAndToys = 5,
        KitchenAndAppliances = 6,
        Other = 7
    }

    public enum MarketplaceListingType
    {
        ForSale = 0,
        FreeGiveaway = 1
    }

    public enum MarketplaceItemCondition
    {
        NewUnopened = 0,
        LikeNew = 1,
        Good = 2,
        Fair = 3
    }

    public enum MarketplaceListingStatus
    {
        PendingApproval = 0,
        Active = 1,
        Sold = 2,
        Withdrawn = 3,
        Rejected = 4
    }

    public class MarketplaceListing
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(1500)]
        public string Description { get; set; } = string.Empty;

        public MarketplaceCategory Category { get; set; } = MarketplaceCategory.FurnitureAndHome;

        public MarketplaceListingType ListingType { get; set; } = MarketplaceListingType.ForSale;

        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceInr { get; set; }

        public MarketplaceItemCondition Condition { get; set; } = MarketplaceItemCondition.Good;

        [ConcurrencyCheck]
        public MarketplaceListingStatus Status { get; set; } = MarketplaceListingStatus.PendingApproval;

        [Required]
        public string SellerUserId { get; set; } = string.Empty;
        public virtual ApplicationUser SellerUser { get; set; } = null!;

        public int FlatId { get; set; }
        public virtual Flat Flat { get; set; } = null!;

        public int BuildingId { get; set; }
        public virtual Building Building { get; set; } = null!;

        [MaxLength(25)]
        public string? SellerContactPhone { get; set; }

        [MaxLength(255)]
        public string? ImageStorageKey { get; set; }

        public string? ModeratedByAdminId { get; set; }
        public virtual ApplicationUser? ModeratedByAdmin { get; set; }
        public DateTime? ModeratedAtUtc { get; set; }

        [MaxLength(500)]
        public string? ModerationNote { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? SoldOrWithdrawnAtUtc { get; set; }

        public virtual ICollection<MarketplaceListingReport> Reports { get; set; } = new List<MarketplaceListingReport>();
    }

    public class MarketplaceListingReport
    {
        public int Id { get; set; }

        public int ListingId { get; set; }
        public virtual MarketplaceListing Listing { get; set; } = null!;

        [Required]
        public string ReporterUserId { get; set; } = string.Empty;
        public virtual ApplicationUser ReporterUser { get; set; } = null!;

        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public bool IsReviewed { get; set; } = false;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}