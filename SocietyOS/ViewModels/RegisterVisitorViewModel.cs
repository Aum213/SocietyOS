using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SocietyOS.ViewModels
{
    public class RegisterVisitorViewModel
    {
        [Required(ErrorMessage = "Visitor name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        [Display(Name = "Visitor Full Name")]
        public string VisitorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a building / tower.")]
        [Display(Name = "Building / Tower")]
        public int BuildingId { get; set; }

        [Required(ErrorMessage = "Please select a destination flat.")]
        [Display(Name = "Destination Flat")]
        public int FlatId { get; set; }

        [Required(ErrorMessage = "Please choose the visit purpose.")]
        [Display(Name = "Purpose of Visit")]
        public string Purpose { get; set; } = "Personal/Guest";

        [StringLength(200, ErrorMessage = "Details cannot exceed 200 characters.")]
        [Display(Name = "Additional Details (Optional)")]
        public string? PurposeDetails { get; set; }

        [Range(0, 20, ErrorMessage = "Accompanying persons must be between 0 and 20.")]
        [Display(Name = "Accompanying Persons (Excluding visitor)")]
        public int AccompanyingPersons { get; set; } = 0;

        [Required(ErrorMessage = "A visitor photo is required.")]
        [Display(Name = "Visitor Photo")]
        public IFormFile Photo { get; set; } = null!;

        public List<SelectListItem> Buildings { get; set; } = new();
        public List<SelectListItem> Flats { get; set; } = new();
    }

    public class VisitorConfirmationViewModel
    {
        public int RequestId { get; set; }
        public string VisitorName { get; set; } = string.Empty;
        public string FlatDisplay { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public int AccompanyingPersons { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}