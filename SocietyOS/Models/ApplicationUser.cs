using System;
using Microsoft.AspNetCore.Identity;

namespace SocietyOS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ResidentProfile? ResidentProfile { get; set; }
        public virtual SecurityGuard? SecurityGuardProfile { get; set; }
    }
}