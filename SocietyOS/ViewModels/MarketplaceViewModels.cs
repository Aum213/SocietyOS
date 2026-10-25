using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class MarketplaceFeedViewModel
    {
        public MarketplaceCategory? SelectedCategory { get; set; }
        public MarketplaceListingType? SelectedType { get; set; }
        public decimal? MaxPriceInr { get; set; }
        public string? SearchQuery { get; set; }
        public string SortBy { get; set; } = "recent"; // "recent", "price-asc", "price-desc"

        public int MyListingsCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalActiveListings { get; set; }

        public List<MarketplaceCardViewModel> Listings { get; set; } = new();
    }

    public class MarketplaceCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DescriptionSnippet { get; set; } = string.Empty;
        public MarketplaceCategory Category { get; set; }
        public string CategoryDisplay { get; set; } = string.Empty;
        public MarketplaceListingType ListingType { get; set; }
        public decimal? PriceInr { get; set; }
        public string PriceDisplay { get; set; } = string.Empty;
        public MarketplaceItemCondition Condition { get; set; }
        public string ConditionDisplay { get; set; } = string.Empty;
        public MarketplaceListingStatus Status { get; set; }
        public string TowerDisplay { get; set; } = string.Empty; // Privacy-safe tower label e.g. "Tower B"
        public string PostedDateDisplay { get; set; } = string.Empty;
        public bool HasImage { get; set; }
        public bool IsOwnListing { get; set; }

        // Admin moderation fields
        public string? SellerInternalDisplay { get; set; }
        public int ReportCount { get; set; }
        public string? ModerationNote { get; set; }
    }

    public class MarketplaceDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MarketplaceCategory Category { get; set; }
        public string CategoryDisplay { get; set; } = string.Empty;
        public MarketplaceListingType ListingType { get; set; }
        public decimal? PriceInr { get; set; }
        public string PriceDisplay { get; set; } = string.Empty;
        public MarketplaceItemCondition Condition { get; set; }
        public string ConditionDisplay { get; set; } = string.Empty;
        public MarketplaceListingStatus Status { get; set; }
        public string TowerDisplay { get; set; } = string.Empty;
        public string PostedDateDisplay { get; set; } = string.Empty;
        public bool HasImage { get; set; }

        public bool IsOwnListing { get; set; }
        public bool IsAdmin { get; set; }
        public string? ModerationNote { get; set; }

        // Intentional Seller Contact Reveal state
        public bool IsContactRevealed { get; set; }
        public string? RevealedSellerName { get; set; }
        public string? RevealedSellerFlat { get; set; }
        public string? RevealedSellerPhone { get; set; }
        public string? RevealedSellerEmail { get; set; }

        // Admin-only report list
        public List<MarketplaceReportItemViewModel> Reports { get; set; } = new();
    }

    public class MarketplaceReportItemViewModel
    {
        public int Id { get; set; }
        public string ReporterName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string CreatedAtDisplay { get; set; } = string.Empty;
    }

    public class MarketplaceListingFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Please enter an item title.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 120 characters.")]
        [Display(Name = "Item Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide an item description.")]
        [StringLength(1500, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 1500 characters.")]
        [Display(Name = "Description & Pickup Details")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Category")]
        public MarketplaceCategory Category { get; set; } = MarketplaceCategory.FurnitureAndHome;

        [Required]
        [Display(Name = "Listing Type")]
        public MarketplaceListingType ListingType { get; set; } = MarketplaceListingType.ForSale;

        [Range(0, 5000000, ErrorMessage = "Price must be between ₹0 and ₹50,00,000.")]
        [Display(Name = "Asking Price (₹ INR)")]
        public decimal? PriceInr { get; set; }

        [Required]
        [Display(Name = "Item Condition")]
        public MarketplaceItemCondition Condition { get; set; } = MarketplaceItemCondition.Good;

        [Phone(ErrorMessage = "Please enter a valid contact phone number.")]
        [StringLength(25)]
        [Display(Name = "Contact Phone for Interested Neighbours (Optional)")]
        public string? SellerContactPhone { get; set; }

        [Display(Name = "Item Photograph (Optional, Max 5MB)")]
        public IFormFile? Image { get; set; }

        public bool HasExistingImage { get; set; }
    }

    public class AdminMarketplaceIndexViewModel
    {
        public MarketplaceListingStatus? SelectedStatus { get; set; }
        public bool FlaggedOnly { get; set; }

        public int PendingCount { get; set; }
        public int ActiveCount { get; set; }
        public int FlaggedCount { get; set; }

        public List<MarketplaceCardViewModel> Listings { get; set; } = new();
    }
}