using System;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public enum ResidentType
    {
        Owner,
        Tenant
    }

    public class ResidentProfile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        public int FlatId { get; set; }
        public virtual Flat Flat { get; set; } = null!;

        public ResidentType ResidentType { get; set; } = ResidentType.Tenant;

        // Zero-trust verification: Users must be verified by an Administrator before gaining flat access
        public bool IsVerified { get; set; } = false;

        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? VerifiedAtUtc { get; set; }
        public string? VerifiedByAdminId { get; set; }
    }
}