using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class EmergencyLandingViewModel
    {
        public string ResidentName { get; set; } = string.Empty;
        public string FlatDisplay { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public bool IsResponder { get; set; }
        public int ActiveIncidentCountForResident { get; set; }

        public List<EmergencyContactItemViewModel> ActiveContacts { get; set; } = new();
        public List<EmergencyIncidentCardViewModel> RecentResidentIncidents { get; set; } = new();
        public CreateEmergencyInputModel Input { get; set; } = new();
    }

    public class CreateEmergencyInputModel
    {
        [Required(ErrorMessage = "Please select the type of emergency.")]
        public EmergencyCategory Category { get; set; } = EmergencyCategory.MedicalEmergency;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [Display(Name = "Optional Situation Details (Do not delay if urgent)")]
        public string? Description { get; set; }
    }

    public class EmergencyContactItemViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class EmergencyIncidentCardViewModel
    {
        public int Id { get; set; }
        public string ReferenceCode { get; set; } = string.Empty;
        public EmergencyCategory Category { get; set; }
        public string CategoryDisplay { get; set; } = string.Empty;
        public EmergencyStatus Status { get; set; }
        public string? Description { get; set; }
        public string CreatedAtLocalDisplay { get; set; } = string.Empty;
        public string NotificationStatusDisplay { get; set; } = string.Empty;

        // Responder fields (populated only for SecurityGuard / Administrator)
        public string? FlatDisplay { get; set; }
        public string? BuildingName { get; set; }
        public string? ResidentName { get; set; }
    }

    public class EmergencyIncidentDetailsViewModel
    {
        public int Id { get; set; }
        public string ReferenceCode { get; set; } = string.Empty;
        public EmergencyCategory Category { get; set; }
        public string CategoryDisplay { get; set; } = string.Empty;
        public EmergencyStatus Status { get; set; }
        public string? Description { get; set; }
        public string FlatDisplay { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public string ResidentName { get; set; } = string.Empty;

        public string NotificationStatusDisplay { get; set; } = string.Empty;
        public string CreatedAtLocalDisplay { get; set; } = string.Empty;
        public string? AcknowledgedAtLocalDisplay { get; set; }
        public string? AcknowledgedByName { get; set; }
        public string? RespondingAtLocalDisplay { get; set; }
        public string? ResponderName { get; set; }
        public string? ResolvedAtLocalDisplay { get; set; }
        public string? ResolvedByName { get; set; }
        public string? ResolutionNote { get; set; }
        public string? CancelledAtLocalDisplay { get; set; }
        public string? CancellationReason { get; set; }

        public bool IsOwnerResident { get; set; }
        public bool IsResponder { get; set; }
        public bool IsAdmin { get; set; }

        public List<EmergencyContactItemViewModel> ActiveContacts { get; set; } = new();
        public List<EmergencyHistoryItemViewModel> History { get; set; } = new();
    }

    public class EmergencyHistoryItemViewModel
    {
        public EmergencyStatus PreviousStatus { get; set; }
        public EmergencyStatus NewStatus { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string ChangedAtLocalDisplay { get; set; } = string.Empty;
    }

    public class ResponderQueueViewModel
    {
        public EmergencyStatus? SelectedStatus { get; set; }
        public EmergencyCategory? SelectedCategory { get; set; }

        public int OpenCount { get; set; }
        public int AcknowledgedCount { get; set; }
        public int RespondingCount { get; set; }
        public int ResolvedCount { get; set; }

        public List<EmergencyIncidentCardViewModel> Incidents { get; set; } = new();
    }

    public class ResponderStatusUpdateInputModel
    {
        [Required]
        public int IncidentId { get; set; }

        [Required]
        public EmergencyStatus NewStatus { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public class AdminEmergencyContactsViewModel
    {
        public List<EmergencyContactItemViewModel> Contacts { get; set; } = new();

        public int? Id { get; set; }

        [Required(ErrorMessage = "Contact display name is required.")]
        [StringLength(100, MinimumLength = 2)]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number or extension is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(25, MinimumLength = 3)]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        [Range(1, 50)]
        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;
    }
}