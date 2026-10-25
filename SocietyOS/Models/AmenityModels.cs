using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public enum AmenityBookingStatus
    {
        Confirmed = 0,
        Cancelled = 1
    }

    public class Amenity
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? Location { get; set; }

        public bool IsActive { get; set; } = true;

        // Operating hours represented in society local time (e.g. 06:00 to 22:00 IST)
        public TimeSpan OpeningTimeLocal { get; set; } = new TimeSpan(6, 0, 0);
        public TimeSpan ClosingTimeLocal { get; set; } = new TimeSpan(22, 0, 0);

        [Range(15, 480)]
        public int MinBookingMinutes { get; set; } = 30;

        [Range(30, 720)]
        public int MaxBookingMinutes { get; set; } = 120;

        [Range(1, 90)]
        public int AdvanceBookingDays { get; set; } = 14;

        [Range(1, 20)]
        public int MaxActiveBookingsPerResident { get; set; } = 2;

        // Capacity = 1 means exclusive slot reservation (e.g. Badminton Court, Hall).
        // Capacity > 1 allows up to Capacity concurrent resident bookings in the same time window (e.g. Gym).
        [Range(1, 100)]
        public int Capacity { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public virtual ICollection<AmenityBooking> Bookings { get; set; } = new List<AmenityBooking>();
        public virtual ICollection<AmenityClosure> Closures { get; set; } = new List<AmenityClosure>();
    }

    public class AmenityBooking
    {
        public int Id { get; set; }

        public int AmenityId { get; set; }
        public virtual Amenity Amenity { get; set; } = null!;

        [Required]
        public string ResidentUserId { get; set; } = string.Empty;
        public virtual ApplicationUser ResidentUser { get; set; } = null!;

        public int FlatId { get; set; }
        public virtual Flat Flat { get; set; } = null!;

        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [ConcurrencyCheck]
        public AmenityBookingStatus Status { get; set; } = AmenityBookingStatus.Confirmed;

        public DateTime? CancelledAtUtc { get; set; }
        public string? CancelledByUserId { get; set; }
        public virtual ApplicationUser? CancelledByUser { get; set; }

        [MaxLength(300)]
        public string? CancellationReason { get; set; }
    }

    public class AmenityClosure
    {
        public int Id { get; set; }

        public int AmenityId { get; set; }
        public virtual Amenity Amenity { get; set; } = null!;

        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }

        [Required, MaxLength(300)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string CreatedByAdminId { get; set; } = string.Empty;
        public virtual ApplicationUser CreatedByAdmin { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}