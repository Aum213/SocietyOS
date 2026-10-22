using System;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public class SecurityGuard
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        [Required, MaxLength(20)]
        public string EmployeeCode { get; set; } = string.Empty; // e.g. "SEC-101"

        [MaxLength(50)]
        public string AssignedGate { get; set; } = "Main Gate 1";

        [MaxLength(20)]
        public string Shift { get; set; } = "Day"; // Day / Night

        public bool IsOnDuty { get; set; } = false;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}