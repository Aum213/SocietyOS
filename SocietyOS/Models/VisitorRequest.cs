using System;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public enum VisitorStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Expired = 3
    }

    public class VisitorRequest
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string VisitorName { get; set; } = string.Empty;

        public int FlatId { get; set; }
        public virtual Flat Flat { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Purpose { get; set; } = "Personal/Guest";

        [MaxLength(200)]
        public string? PurposeDetails { get; set; }

        [Range(0, 20)]
        public int AccompanyingPersons { get; set; } = 0;

        [Required, MaxLength(255)]
        public string PhotoStorageKey { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Derived strictly as CreatedAtUtc + 48 hours; never reset
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(48);

        // Concurrency token prevents simultaneous conflicting decisions
        [ConcurrencyCheck]
        public VisitorStatus Status { get; set; } = VisitorStatus.Pending;

        [Required]
        public string RegisteredByGuardId { get; set; } = string.Empty;
        public virtual ApplicationUser RegisteredByGuard { get; set; } = null!;

        // Decision tracking
        public DateTime? DecisionAtUtc { get; set; }
        public string? DecisionByResidentId { get; set; }
        public virtual ApplicationUser? DecisionByResident { get; set; }

        [MaxLength(200)]
        public string? DecisionNotes { get; set; }
    }
}