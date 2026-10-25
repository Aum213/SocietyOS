using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class AmenityCatalogueViewModel
    {
        public string ResidentFlatDisplay { get; set; } = string.Empty;
        public int ActiveUpcomingBookingsCount { get; set; }
        public List<AmenityItemViewModel> Amenities { get; set; } = new();
    }

    public class AmenityItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Location { get; set; }
        public bool IsActive { get; set; }
        public string OperatingHoursDisplay { get; set; } = string.Empty;
        public int MinBookingMinutes { get; set; }
        public int MaxBookingMinutes { get; set; }
        public int AdvanceBookingDays { get; set; }
        public int MaxActiveBookingsPerResident { get; set; }
        public int Capacity { get; set; }
        public int UpcomingConfirmedCount { get; set; }
    }

    public class AmenityDetailsViewModel
    {
        public AmenityItemViewModel Amenity { get; set; } = new();
        public DateOnly SelectedLocalDate { get; set; }
        public DateOnly MinLocalDate { get; set; }
        public DateOnly MaxLocalDate { get; set; }
        public int ResidentActiveBookingsForAmenity { get; set; }

        public List<OccupiedIntervalViewModel> OccupiedIntervals { get; set; } = new();
        public List<ClosureIntervalViewModel> Closures { get; set; } = new();
        public List<string> AvailableTimeOptions { get; set; } = new();

        public CreateBookingInputModel BookingForm { get; set; } = new();
    }

    public class OccupiedIntervalViewModel
    {
        public string StartLocalDisplay { get; set; } = string.Empty;
        public string EndLocalDisplay { get; set; } = string.Empty;
        public int BookedCount { get; set; }
        public int Capacity { get; set; }
        public bool IsCurrentUserBooking { get; set; }
        public bool IsFull => BookedCount >= Capacity;
    }

    public class ClosureIntervalViewModel
    {
        public int Id { get; set; }
        public string StartLocalDisplay { get; set; } = string.Empty;
        public string EndLocalDisplay { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class CreateBookingInputModel
    {
        [Required]
        public int AmenityId { get; set; }

        [Required(ErrorMessage = "Please choose a booking date.")]
        public DateOnly LocalDate { get; set; }

        [Required(ErrorMessage = "Please choose a start time.")]
        public string StartTimeLocal { get; set; } = "18:00";

        [Required(ErrorMessage = "Please choose an end time.")]
        public string EndTimeLocal { get; set; } = "19:00";
    }

    public class MyBookingsViewModel
    {
        public string FlatDisplay { get; set; } = string.Empty;
        public List<BookingRecordViewModel> UpcomingBookings { get; set; } = new();
        public List<BookingRecordViewModel> PastOrCancelledBookings { get; set; } = new();
    }

    public class BookingRecordViewModel
    {
        public int Id { get; set; }
        public int AmenityId { get; set; }
        public string AmenityName { get; set; } = string.Empty;
        public string? AmenityLocation { get; set; }
        public string LocalDateDisplay { get; set; } = string.Empty;
        public string LocalTimeRangeDisplay { get; set; } = string.Empty;
        public AmenityBookingStatus Status { get; set; }
        public bool CanCancel { get; set; }
        public string? CancellationReason { get; set; }
        public string? CancelledAtLocalDisplay { get; set; }

        // Admin-only fields
        public string? ResidentName { get; set; }
        public string? FlatDisplay { get; set; }
    }

    public class AmenityFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Amenity name is required.")]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(1000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        [StringLength(120)]
        public string? Location { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        [Display(Name = "Opening Time (Local IST)")]
        public TimeSpan OpeningTimeLocal { get; set; } = new TimeSpan(6, 0, 0);

        [Required]
        [Display(Name = "Closing Time (Local IST)")]
        public TimeSpan ClosingTimeLocal { get; set; } = new TimeSpan(22, 0, 0);

        [Range(15, 480)]
        [Display(Name = "Min Booking Duration (Minutes)")]
        public int MinBookingMinutes { get; set; } = 30;

        [Range(30, 720)]
        [Display(Name = "Max Booking Duration (Minutes)")]
        public int MaxBookingMinutes { get; set; } = 120;

        [Range(1, 90)]
        [Display(Name = "Advance Booking Window (Days)")]
        public int AdvanceBookingDays { get; set; } = 14;

        [Range(1, 20)]
        [Display(Name = "Max Active Bookings Per Resident")]
        public int MaxActiveBookingsPerResident { get; set; } = 2;

        [Range(1, 100)]
        [Display(Name = "Concurrent Slot Capacity (1 = Exclusive)")]
        public int Capacity { get; set; } = 1;
    }

    public class AdminBookingsIndexViewModel
    {
        public int? SelectedAmenityId { get; set; }
        public AmenityBookingStatus? SelectedStatus { get; set; }
        public DateOnly? SelectedLocalDate { get; set; }

        public List<AmenityItemViewModel> Amenities { get; set; } = new();
        public List<BookingRecordViewModel> Bookings { get; set; } = new();
    }

    public class AdminClosuresViewModel
    {
        public AmenityItemViewModel Amenity { get; set; } = new();
        public List<ClosureIntervalViewModel> Closures { get; set; } = new();

        [Required]
        public int AmenityId { get; set; }

        [Required]
        public DateOnly StartLocalDate { get; set; }

        [Required]
        public TimeOnly StartLocalTime { get; set; } = new TimeOnly(9, 0);

        [Required]
        public DateOnly EndLocalDate { get; set; }

        [Required]
        public TimeOnly EndLocalTime { get; set; } = new TimeOnly(17, 0);

        [Required, StringLength(300, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }
}