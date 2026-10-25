using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public enum EmergencyCategory
    {
        MedicalEmergency = 0,
        FireOrSmoke = 1,
        SecurityOrSafety = 2,
        LiftEntrapment = 3,
        OtherEmergency = 4
    }

    public enum EmergencyStatus
    {
        Open = 0,
        Acknowledged = 1,
        Responding = 2,
        Resolved = 3,
        Cancelled = 4
    }

    public enum EmergencyNotificationStatus
    {
        InAppQueueOnly = 0
    }

    public enum IntercomCategory
    {
        SecurityDesk = 0,
        ManagementOffice = 1,
        Maintenance = 2,
        Housekeeping = 3,
        OtherSocietyService = 4
    }

    public class EmergencyIncident
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string ReferenceCode { get; set; } = string.Empty;

        [Required]
        public string ReporterUserId { get; set; } = string.Empty;
        public virtual ApplicationUser ReporterUser { get; set; } = null!;

        public int FlatId { get; set; }
        public virtual Flat Flat { get; set; } = null!;

        public int BuildingId { get; set; }
        public virtual Building Building { get; set; } = null!;

        public EmergencyCategory Category { get; set; } = EmergencyCategory.SecurityOrSafety;

        [MaxLength(500)]
        public string? Description { get; set; }

        [ConcurrencyCheck]
        public EmergencyStatus Status { get; set; } = EmergencyStatus.Open;

        public EmergencyNotificationStatus NotificationStatus { get; set; } = EmergencyNotificationStatus.InAppQueueOnly;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? AcknowledgedAtUtc { get; set; }
        public string? AcknowledgedByUserId { get; set; }
        public virtual ApplicationUser? AcknowledgedByUser { get; set; }

        public DateTime? RespondingAtUtc { get; set; }
        public string? ResponderUserId { get; set; }
        public virtual ApplicationUser? ResponderUser { get; set; }

        public DateTime? ResolvedAtUtc { get; set; }
        public string? ResolvedByUserId { get; set; }
        public virtual ApplicationUser? ResolvedByUser { get; set; }

        [MaxLength(500)]
        public string? ResolutionNote { get; set; }

        public DateTime? CancelledAtUtc { get; set; }
        public string? CancelledByUserId { get; set; }
        public virtual ApplicationUser? CancelledByUser { get; set; }

        [MaxLength(300)]
        public string? CancellationReason { get; set; }

        public virtual ICollection<EmergencyStatusHistory> StatusHistory { get; set; } = new List<EmergencyStatusHistory>();
    }

    public class EmergencyStatusHistory
    {
        public int Id { get; set; }

        public int IncidentId { get; set; }
        public virtual EmergencyIncident Incident { get; set; } = null!;

        public EmergencyStatus PreviousStatus { get; set; }
        public EmergencyStatus NewStatus { get; set; }

        [Required]
        public string ActorUserId { get; set; } = string.Empty;
        public virtual ApplicationUser ActorUser { get; set; } = null!;

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class SocietyEmergencyContact
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        public IntercomCategory Category { get; set; } = IntercomCategory.SecurityDesk;

        [Required, MaxLength(25)]
        public string PhoneNumber { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        // If true, this contact also appears on the Emergency SOS screen
        public bool ShowOnEmergencySos { get; set; } = true;

        [Range(1, 50)]
        public int DisplayOrder { get; set; } = 1;

        public string? UpdatedByAdminId { get; set; }
        public virtual ApplicationUser? UpdatedByAdmin { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}