using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class IntercomDirectoryViewModel
    {
        public IntercomCategory? SelectedCategory { get; set; }
        public string? SearchQuery { get; set; }
        public List<IntercomContactItemViewModel> Contacts { get; set; } = new();
    }

    public class IntercomContactItemViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public IntercomCategory Category { get; set; }
        public string CategoryDisplay { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool ShowOnEmergencySos { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class IntercomContactFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Contact or department name is required.")]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Contact / Department Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Service Category")]
        public IntercomCategory Category { get; set; } = IntercomCategory.SecurityDesk;

        [Required(ErrorMessage = "Phone number or intercom extension is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone or intercom number.")]
        [StringLength(25, MinimumLength = 3)]
        [Display(Name = "Phone / Intercom Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Short Description / Timings (Optional)")]
        public string? Description { get; set; }

        [Range(1, 50)]
        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; } = 1;

        [Display(Name = "Active in Intercom Directory")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Also show on Emergency SOS page")]
        public bool ShowOnEmergencySos { get; set; } = false;
    }

    public class AdminIntercomIndexViewModel
    {
        public List<IntercomContactItemViewModel> Contacts { get; set; } = new();
        public IntercomContactFormViewModel NewContact { get; set; } = new();
    }
}