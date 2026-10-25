using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocietyOS.Data;
using SocietyOS.Models;
using SocietyOS.Services;
using SocietyOS.ViewModels;

namespace SocietyOS.Controllers
{
    [Authorize(Roles = "Resident,Administrator")]
    public class AmenitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISocietyTimeZoneService _tz;
        private readonly ILogger<AmenitiesController> _logger;

        public AmenitiesController(
            ApplicationDbContext context,
            ISocietyTimeZoneService tz,
            ILogger<AmenitiesController> logger)
        {
            _context = context;
            _tz = tz;
            _logger = logger;
        }

        private async Task<ResidentProfile?> GetVerifiedResidentProfileAsync(CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return null;

            return await _context.ResidentProfiles
                .AsNoTracking()
                .Include(r => r.Flat)
                    .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(r => r.UserId == currentUserId && r.IsVerified, ct);
        }

        private static string FormatTimeSpanAmPm(TimeSpan ts)
        {
            return DateTime.Today.Add(ts).ToString("hh:mm tt");
        }

        private static List<string> BuildTimeOptions(TimeSpan open, TimeSpan close)
        {
            var list = new List<string>();
            for (var t = open; t <= close; t = t.Add(TimeSpan.FromMinutes(30)))
            {
                list.Add($"{(int)t.TotalHours:D2}:{t.Minutes:D2}");
            }
            return list;
        }

        // ==================================================================
        // GET: /Amenities (Resident Amenity Catalogue)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            var resident = await GetVerifiedResidentProfileAsync(ct);

            if (!isAdmin && resident == null)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var nowUtc = _tz.UtcNow;

            var myUpcomingCount = await _context.AmenityBookings
                .AsNoTracking()
                .CountAsync(b => b.ResidentUserId == currentUserId &&
                                 b.Status == AmenityBookingStatus.Confirmed &&
                                 b.EndTimeUtc > nowUtc, ct);

            var amenities = await _context.Amenities
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .Select(a => new AmenityItemViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    Location = a.Location,
                    IsActive = a.IsActive,
                    OperatingHoursDisplay = $"{FormatTimeSpanAmPm(a.OpeningTimeLocal)} – {FormatTimeSpanAmPm(a.ClosingTimeLocal)} IST",
                    MinBookingMinutes = a.MinBookingMinutes,
                    MaxBookingMinutes = a.MaxBookingMinutes,
                    AdvanceBookingDays = a.AdvanceBookingDays,
                    MaxActiveBookingsPerResident = a.MaxActiveBookingsPerResident,
                    Capacity = a.Capacity
                })
                .ToListAsync(ct);

            var model = new AmenityCatalogueViewModel
            {
                ResidentFlatDisplay = resident != null
                    ? $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}"
                    : "Admin Preview",
                ActiveUpcomingBookingsCount = myUpcomingCount,
                Amenities = amenities
            };

            return View(model);
        }

        // ==================================================================
        // GET: /Amenities/Details/{id}?date=YYYY-MM-DD
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id, string? date, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            var resident = await GetVerifiedResidentProfileAsync(ct);

            if (!isAdmin && resident == null)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var amenity = await _context.Amenities
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id && (a.IsActive || isAdmin), ct);

            if (amenity == null) return NotFound();

            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);
            var maxLocalDate = todayLocal.AddDays(amenity.AdvanceBookingDays);

            if (!DateOnly.TryParse(date, out var selectedDate) || selectedDate < todayLocal)
            {
                selectedDate = todayLocal;
            }
            if (selectedDate > maxLocalDate)
            {
                selectedDate = maxLocalDate;
            }

            var dayStartUtc = _tz.ToUtc(selectedDate, TimeOnly.MinValue);
            var dayEndUtc = _tz.ToUtc(selectedDate.AddDays(1), TimeOnly.MinValue);
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Load confirmed bookings overlapping this local calendar day
            var dayBookings = await _context.AmenityBookings
                .AsNoTracking()
                .Where(b => b.AmenityId == id &&
                            b.Status == AmenityBookingStatus.Confirmed &&
                            b.StartTimeUtc < dayEndUtc &&
                            b.EndTimeUtc > dayStartUtc)
                .OrderBy(b => b.StartTimeUtc)
                .ToListAsync(ct);

            var occupied = dayBookings
                .GroupBy(b => new { b.StartTimeUtc, b.EndTimeUtc })
                .Select(g => new OccupiedIntervalViewModel
                {
                    StartLocalDisplay = _tz.ToLocalTime(g.Key.StartTimeUtc).ToString("hh:mm tt"),
                    EndLocalDisplay = _tz.ToLocalTime(g.Key.EndTimeUtc).ToString("hh:mm tt"),
                    BookedCount = g.Count(),
                    Capacity = amenity.Capacity,
                    IsCurrentUserBooking = g.Any(x => x.ResidentUserId == currentUserId)
                })
                .ToList();

            var closures = await _context.AmenityClosures
                .AsNoTracking()
                .Where(c => c.AmenityId == id &&
                            c.StartTimeUtc < dayEndUtc &&
                            c.EndTimeUtc > dayStartUtc)
                .OrderBy(c => c.StartTimeUtc)
                .ToListAsync(ct);

            var closureItems = closures.Select(c => new ClosureIntervalViewModel
            {
                Id = c.Id,
                StartLocalDisplay = _tz.ToLocalTime(c.StartTimeUtc).ToString("dd MMM hh:mm tt"),
                EndLocalDisplay = _tz.ToLocalTime(c.EndTimeUtc).ToString("dd MMM hh:mm tt"),
                Reason = c.Reason
            }).ToList();

            var activeResidentCount = await _context.AmenityBookings
                .AsNoTracking()
                .CountAsync(b => b.AmenityId == id &&
                                 b.ResidentUserId == currentUserId &&
                                 b.Status == AmenityBookingStatus.Confirmed &&
                                 b.EndTimeUtc > _tz.UtcNow, ct);

            var timeOptions = BuildTimeOptions(amenity.OpeningTimeLocal, amenity.ClosingTimeLocal);
            var defaultStart = timeOptions.FirstOrDefault() ?? "06:00";
            var defaultEnd = timeOptions.Skip(2).FirstOrDefault() ?? timeOptions.LastOrDefault() ?? "07:00";

            var model = new AmenityDetailsViewModel
            {
                Amenity = new AmenityItemViewModel
                {
                    Id = amenity.Id,
                    Name = amenity.Name,
                    Description = amenity.Description,
                    Location = amenity.Location,
                    IsActive = amenity.IsActive,
                    OperatingHoursDisplay = $"{FormatTimeSpanAmPm(amenity.OpeningTimeLocal)} – {FormatTimeSpanAmPm(amenity.ClosingTimeLocal)} IST",
                    MinBookingMinutes = amenity.MinBookingMinutes,
                    MaxBookingMinutes = amenity.MaxBookingMinutes,
                    AdvanceBookingDays = amenity.AdvanceBookingDays,
                    MaxActiveBookingsPerResident = amenity.MaxActiveBookingsPerResident,
                    Capacity = amenity.Capacity
                },
                SelectedLocalDate = selectedDate,
                MinLocalDate = todayLocal,
                MaxLocalDate = maxLocalDate,
                ResidentActiveBookingsForAmenity = activeResidentCount,
                OccupiedIntervals = occupied,
                Closures = closureItems,
                AvailableTimeOptions = timeOptions,
                BookingForm = new CreateBookingInputModel
                {
                    AmenityId = amenity.Id,
                    LocalDate = selectedDate,
                    StartTimeLocal = defaultStart,
                    EndTimeLocal = defaultEnd
                }
            };

            return View(model);
        }

        // ==================================================================
        // POST: /Amenities/Book (Concurrency-Safe Slot Reservation)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book([FromForm] CreateBookingInputModel input, CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null)
            {
                TempData["ErrorMessage"] = "Only verified residents with an assigned flat may reserve society amenities.";
                return RedirectToAction(nameof(Index));
            }

            if (!TimeOnly.TryParse(input.StartTimeLocal, out var startLocalTime) ||
                !TimeOnly.TryParse(input.EndTimeLocal, out var endLocalTime))
            {
                TempData["ErrorMessage"] = "Please select valid start and end times.";
                return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
            }

            if (endLocalTime <= startLocalTime)
            {
                TempData["ErrorMessage"] = "End time must be after the start time.";
                return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
            }

            DateTime reqStartUtc;
            DateTime reqEndUtc;
            try
            {
                reqStartUtc = _tz.ToUtc(input.LocalDate, startLocalTime);
                reqEndUtc = _tz.ToUtc(input.LocalDate, endLocalTime);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
            }

            var nowUtc = _tz.UtcNow;
            if (reqStartUtc <= nowUtc)
            {
                TempData["ErrorMessage"] = "Cannot book a time slot in the past. Please choose a future start time.";
                return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
            }

            var durationMinutes = (int)Math.Round((reqEndUtc - reqStartUtc).TotalMinutes);

            // Begin transaction and acquire an exclusive row lock (UPDLOCK, HOLDLOCK) on the parent Amenity row
            // so concurrent booking requests for the same amenity are strictly serialized by SQL Server.
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM Amenities WITH (UPDLOCK, HOLDLOCK) WHERE Id = {0}",
                    new object[] { input.AmenityId },
                    ct);

                var amenity = await _context.Amenities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == input.AmenityId, ct);

                if (amenity == null || !amenity.IsActive)
                {
                    await transaction.RollbackAsync(ct);
                    TempData["ErrorMessage"] = "This amenity is currently inactive and unavailable for booking.";
                    return RedirectToAction(nameof(Index));
                }

                // 1. Validate operating hours in society local time
                var startSpan = startLocalTime.ToTimeSpan();
                var endSpan = endLocalTime.ToTimeSpan();
                if (startSpan < amenity.OpeningTimeLocal || endSpan > amenity.ClosingTimeLocal)
                {
                    await transaction.RollbackAsync(ct);
                    TempData["ErrorMessage"] = $"Selected slot is outside operating hours ({FormatTimeSpanAmPm(amenity.OpeningTimeLocal)} – {FormatTimeSpanAmPm(amenity.ClosingTimeLocal)} IST).";
                    return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                }

                // 2. Validate duration limits
                if (durationMinutes < amenity.MinBookingMinutes || durationMinutes > amenity.MaxBookingMinutes)
                {
                    await transaction.RollbackAsync(ct);
                    TempData["ErrorMessage"] = $"Booking duration must be between {amenity.MinBookingMinutes} and {amenity.MaxBookingMinutes} minutes.";
                    return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                }

                // 3. Validate advance booking window
                var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);
                var maxAllowedDate = todayLocal.AddDays(amenity.AdvanceBookingDays);
                if (input.LocalDate < todayLocal || input.LocalDate > maxAllowedDate)
                {
                    await transaction.RollbackAsync(ct);
                    TempData["ErrorMessage"] = $"Bookings can only be made up to {amenity.AdvanceBookingDays} days in advance.";
                    return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                }

                // 4. Validate maintenance closures using interval overlap: ClosureStart < ReqEnd && ClosureEnd > ReqStart
                var overlappingClosure = await _context.AmenityClosures
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.AmenityId == amenity.Id &&
                                              c.StartTimeUtc < reqEndUtc &&
                                              c.EndTimeUtc > reqStartUtc, ct);

                if (overlappingClosure != null)
                {
                    await transaction.RollbackAsync(ct);
                    TempData["ErrorMessage"] = $"This amenity is closed during the requested time ({overlappingClosure.Reason}).";
                    return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                }

                // 5. Validate resident's active booking limit for this amenity
                var userActiveCount = await _context.AmenityBookings
                    .CountAsync(b => b.AmenityId == amenity.Id &&
                                     b.ResidentUserId == resident.UserId &&
                                     b.Status == AmenityBookingStatus.Confirmed &&
                                     b.EndTimeUtc > nowUtc, ct);

                if (userActiveCount >= amenity.MaxActiveBookingsPerResident)
                {
                    await transaction.RollbackAsync(ct);
                    TempData["ErrorMessage"] = $"You already have the maximum of {amenity.MaxActiveBookingsPerResident} active upcoming bookings for {amenity.Name}.";
                    return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                }

                // 6. Prevent same resident from double-booking overlapping slots
                var residentAlreadyInSlot = await _context.AmenityBookings
                    .AnyAsync(b => b.AmenityId == amenity.Id &&
                                   b.ResidentUserId == resident.UserId &&
                                   b.Status == AmenityBookingStatus.Confirmed &&
                                   b.StartTimeUtc < reqEndUtc &&
                                   b.EndTimeUtc > reqStartUtc, ct);

                if (residentAlreadyInSlot)
                {
                    await transaction.RollbackAsync(ct);
                    TempData["ErrorMessage"] = "You already have a confirmed reservation overlapping this time window.";
                    return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                }

                // 7. Standard Interval Overlap Rule: ExistingStart < RequestedEnd AND ExistingEnd > RequestedStart
                // Note: Adjacent bookings where ExistingEnd == RequestedStart do NOT overlap.
                var overlappingBookings = await _context.AmenityBookings
                    .AsNoTracking()
                    .Where(b => b.AmenityId == amenity.Id &&
                                b.Status == AmenityBookingStatus.Confirmed &&
                                b.StartTimeUtc < reqEndUtc &&
                                b.EndTimeUtc > reqStartUtc)
                    .ToListAsync(ct);

                if (amenity.Capacity == 1)
                {
                    if (overlappingBookings.Count > 0)
                    {
                        await transaction.RollbackAsync(ct);
                        TempData["ErrorMessage"] = "This time slot is unavailable because it overlaps an existing reservation.";
                        return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                    }
                }
                else
                {
                    // For multi-capacity amenities (e.g. Gym), verify every 15-minute slice in [reqStartUtc, reqEndUtc) is < Capacity
                    for (var slice = reqStartUtc; slice < reqEndUtc; slice = slice.AddMinutes(15))
                    {
                        var sliceEnd = slice.AddMinutes(15);
                        var concurrentInSlice = overlappingBookings.Count(b => b.StartTimeUtc < sliceEnd && b.EndTimeUtc > slice);
                        if (concurrentInSlice >= amenity.Capacity)
                        {
                            await transaction.RollbackAsync(ct);
                            TempData["ErrorMessage"] = $"Maximum capacity ({amenity.Capacity}) has been reached for part or all of the selected time slot.";
                            return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
                        }
                    }
                }

                var booking = new AmenityBooking
                {
                    AmenityId = amenity.Id,
                    ResidentUserId = resident.UserId,
                    FlatId = resident.FlatId,
                    StartTimeUtc = reqStartUtc,
                    EndTimeUtc = reqEndUtc,
                    CreatedAtUtc = nowUtc,
                    Status = AmenityBookingStatus.Confirmed
                };

                _context.AmenityBookings.Add(booking);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                var localStartStr = _tz.ToLocalTime(reqStartUtc).ToString("dd MMM yyyy, hh:mm tt");
                var localEndStr = _tz.ToLocalTime(reqEndUtc).ToString("hh:mm tt");
                TempData["SuccessMessage"] = $"Confirmed! {amenity.Name} reserved for {localStartStr} – {localEndStr} IST.";

                return RedirectToAction(nameof(MyBookings));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Unexpected failure while booking amenity {AmenityId}.", input.AmenityId);
                TempData["ErrorMessage"] = "Could not complete your reservation due to a concurrent update. Please try again.";
                return RedirectToAction(nameof(Details), new { id = input.AmenityId, date = input.LocalDate.ToString("yyyy-MM-dd") });
            }
        }

        // ==================================================================
        // GET: /Amenities/MyBookings (Resident's Personal Reservations)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> MyBookings(CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            var resident = await GetVerifiedResidentProfileAsync(ct);

            if (!isAdmin && resident == null)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var nowUtc = _tz.UtcNow;

            var records = await _context.AmenityBookings
                .AsNoTracking()
                .Include(b => b.Amenity)
                .Where(b => b.ResidentUserId == currentUserId)
                .OrderByDescending(b => b.StartTimeUtc)
                .ToListAsync(ct);

            var allMapped = records.Select(b =>
            {
                var startLocal = _tz.ToLocalTime(b.StartTimeUtc);
                var endLocal = _tz.ToLocalTime(b.EndTimeUtc);
                var isFutureConfirmed = b.Status == AmenityBookingStatus.Confirmed && b.StartTimeUtc > nowUtc;

                return new BookingRecordViewModel
                {
                    Id = b.Id,
                    AmenityId = b.AmenityId,
                    AmenityName = b.Amenity.Name,
                    AmenityLocation = b.Amenity.Location,
                    LocalDateDisplay = startLocal.ToString("ddd, dd MMM yyyy"),
                    LocalTimeRangeDisplay = $"{startLocal:hh:mm tt} – {endLocal:hh:mm tt} IST",
                    Status = b.Status,
                    CanCancel = isFutureConfirmed,
                    CancellationReason = b.CancellationReason,
                    CancelledAtLocalDisplay = b.CancelledAtUtc.HasValue
                        ? _tz.ToLocalTime(b.CancelledAtUtc.Value).ToString("dd MMM, hh:mm tt")
                        : null
                };
            }).ToList();

            var model = new MyBookingsViewModel
            {
                FlatDisplay = resident != null
                    ? $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}"
                    : "Admin Account",
                UpcomingBookings = allMapped
                    .Where(b => b.Status == AmenityBookingStatus.Confirmed && b.CanCancel)
                    .OrderBy(b => b.LocalDateDisplay)
                    .ToList(),
                PastOrCancelledBookings = allMapped
                    .Where(b => !(b.Status == AmenityBookingStatus.Confirmed && b.CanCancel))
                    .ToList()
            };

            return View(model);
        }

        // ==================================================================
        // POST: /Amenities/Cancel (Resident or Admin Cancellation)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason, bool fromAdmin = false, CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isAdmin = User.IsInRole("Administrator");

            var booking = await _context.AmenityBookings
                .Include(b => b.Amenity)
                .FirstOrDefaultAsync(b => b.Id == id, ct);

            if (booking == null) return NotFound();

            // Strict ownership check: non-admin residents can ONLY cancel their own bookings
            if (!isAdmin && booking.ResidentUserId != currentUserId)
            {
                return NotFound();
            }

            if (booking.Status == AmenityBookingStatus.Cancelled)
            {
                TempData["WarningMessage"] = "This booking has already been cancelled.";
                return RedirectToAction(fromAdmin && isAdmin ? nameof(Bookings) : nameof(MyBookings));
            }

            if (!isAdmin && booking.StartTimeUtc <= _tz.UtcNow)
            {
                TempData["ErrorMessage"] = "Past or in-progress bookings cannot be cancelled.";
                return RedirectToAction(nameof(MyBookings));
            }

            booking.Status = AmenityBookingStatus.Cancelled;
            booking.CancelledAtUtc = _tz.UtcNow;
            booking.CancelledByUserId = currentUserId;
            booking.CancellationReason = !string.IsNullOrWhiteSpace(reason)
                ? reason.Trim()
                : (isAdmin && booking.ResidentUserId != currentUserId
                    ? "Cancelled by Society Administration"
                    : "Cancelled by Resident");

            try
            {
                await _context.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = $"Reservation for {booking.Amenity.Name} has been cancelled and the slot is now free.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["WarningMessage"] = "Booking status was already updated.";
            }

            return RedirectToAction(fromAdmin && isAdmin ? nameof(Bookings) : nameof(MyBookings));
        }

        // ==================================================================
        // GET: /Amenities/Admin (Administrator Amenity Configuration Console)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Admin(CancellationToken ct = default)
        {
            var nowUtc = _tz.UtcNow;
            var amenities = await _context.Amenities
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .Select(a => new AmenityItemViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    Location = a.Location,
                    IsActive = a.IsActive,
                    OperatingHoursDisplay = $"{FormatTimeSpanAmPm(a.OpeningTimeLocal)} – {FormatTimeSpanAmPm(a.ClosingTimeLocal)} IST",
                    MinBookingMinutes = a.MinBookingMinutes,
                    MaxBookingMinutes = a.MaxBookingMinutes,
                    AdvanceBookingDays = a.AdvanceBookingDays,
                    MaxActiveBookingsPerResident = a.MaxActiveBookingsPerResident,
                    Capacity = a.Capacity,
                    UpcomingConfirmedCount = a.Bookings.Count(b => b.Status == AmenityBookingStatus.Confirmed && b.EndTimeUtc > nowUtc)
                })
                .ToListAsync(ct);

            return View(amenities);
        }

        // ==================================================================
        // GET & POST: /Amenities/Create (Admin Create Amenity)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AmenityFormViewModel());
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AmenityFormViewModel model, CancellationToken ct = default)
        {
            if (model.ClosingTimeLocal <= model.OpeningTimeLocal)
            {
                ModelState.AddModelError(nameof(model.ClosingTimeLocal), "Closing time must be after opening time.");
            }
            if (model.MaxBookingMinutes < model.MinBookingMinutes)
            {
                ModelState.AddModelError(nameof(model.MaxBookingMinutes), "Maximum duration cannot be less than minimum duration.");
            }

            if (!ModelState.IsValid) return View(model);

            var nowUtc = _tz.UtcNow;
            var amenity = new Amenity
            {
                Name = model.Name.Trim(),
                Description = model.Description.Trim(),
                Location = model.Location?.Trim(),
                IsActive = model.IsActive,
                OpeningTimeLocal = model.OpeningTimeLocal,
                ClosingTimeLocal = model.ClosingTimeLocal,
                MinBookingMinutes = model.MinBookingMinutes,
                MaxBookingMinutes = model.MaxBookingMinutes,
                AdvanceBookingDays = model.AdvanceBookingDays,
                MaxActiveBookingsPerResident = model.MaxActiveBookingsPerResident,
                Capacity = model.Capacity,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _context.Amenities.Add(amenity);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = $"Amenity '{amenity.Name}' created.";
            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // GET & POST: /Amenities/Edit/{id} (Admin Edit Amenity)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
        {
            var a = await _context.Amenities.FindAsync(new object[] { id }, ct);
            if (a == null) return NotFound();

            var model = new AmenityFormViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                Location = a.Location,
                IsActive = a.IsActive,
                OpeningTimeLocal = a.OpeningTimeLocal,
                ClosingTimeLocal = a.ClosingTimeLocal,
                MinBookingMinutes = a.MinBookingMinutes,
                MaxBookingMinutes = a.MaxBookingMinutes,
                AdvanceBookingDays = a.AdvanceBookingDays,
                MaxActiveBookingsPerResident = a.MaxActiveBookingsPerResident,
                Capacity = a.Capacity
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AmenityFormViewModel model, CancellationToken ct = default)
        {
            if (id != model.Id) return BadRequest();

            if (model.ClosingTimeLocal <= model.OpeningTimeLocal)
            {
                ModelState.AddModelError(nameof(model.ClosingTimeLocal), "Closing time must be after opening time.");
            }
            if (model.MaxBookingMinutes < model.MinBookingMinutes)
            {
                ModelState.AddModelError(nameof(model.MaxBookingMinutes), "Maximum duration cannot be less than minimum duration.");
            }

            if (!ModelState.IsValid) return View(model);

            var amenity = await _context.Amenities.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (amenity == null) return NotFound();

            amenity.Name = model.Name.Trim();
            amenity.Description = model.Description.Trim();
            amenity.Location = model.Location?.Trim();
            amenity.IsActive = model.IsActive;
            amenity.OpeningTimeLocal = model.OpeningTimeLocal;
            amenity.ClosingTimeLocal = model.ClosingTimeLocal;
            amenity.MinBookingMinutes = model.MinBookingMinutes;
            amenity.MaxBookingMinutes = model.MaxBookingMinutes;
            amenity.AdvanceBookingDays = model.AdvanceBookingDays;
            amenity.MaxActiveBookingsPerResident = model.MaxActiveBookingsPerResident;
            amenity.Capacity = model.Capacity;
            amenity.UpdatedAtUtc = _tz.UtcNow;

            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = $"Amenity '{amenity.Name}' updated.";
            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // POST: /Amenities/ToggleActive/{id}
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, CancellationToken ct = default)
        {
            var amenity = await _context.Amenities.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (amenity == null) return NotFound();

            amenity.IsActive = !amenity.IsActive;
            amenity.UpdatedAtUtc = _tz.UtcNow;
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = amenity.IsActive
                ? $"'{amenity.Name}' is now active for resident bookings."
                : $"'{amenity.Name}' deactivated. New bookings are disabled while historical records remain intact.";

            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // GET: /Amenities/Bookings (Admin Reservations Review & Management)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Bookings(
            int? amenityId,
            AmenityBookingStatus? status,
            string? date,
            CancellationToken ct = default)
        {
            var amenities = await _context.Amenities
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .Select(a => new AmenityItemViewModel { Id = a.Id, Name = a.Name })
                .ToListAsync(ct);

            var query = _context.AmenityBookings
                .AsNoTracking()
                .Include(b => b.Amenity)
                .Include(b => b.ResidentUser)
                .Include(b => b.Flat)
                    .ThenInclude(f => f.Building)
                .AsQueryable();

            if (amenityId.HasValue)
            {
                query = query.Where(b => b.AmenityId == amenityId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(b => b.Status == status.Value);
            }

            DateOnly? parsedDate = null;
            if (DateOnly.TryParse(date, out var d))
            {
                parsedDate = d;
                var startUtc = _tz.ToUtc(d, TimeOnly.MinValue);
                var endUtc = _tz.ToUtc(d.AddDays(1), TimeOnly.MinValue);
                query = query.Where(b => b.StartTimeUtc < endUtc && b.EndTimeUtc > startUtc);
            }

            var rawBookings = await query
                .OrderByDescending(b => b.StartTimeUtc)
                .Take(100)
                .ToListAsync(ct);

            var nowUtc = _tz.UtcNow;
            var mapped = rawBookings.Select(b =>
            {
                var startLocal = _tz.ToLocalTime(b.StartTimeUtc);
                var endLocal = _tz.ToLocalTime(b.EndTimeUtc);
                return new BookingRecordViewModel
                {
                    Id = b.Id,
                    AmenityId = b.AmenityId,
                    AmenityName = b.Amenity.Name,
                    LocalDateDisplay = startLocal.ToString("ddd, dd MMM yyyy"),
                    LocalTimeRangeDisplay = $"{startLocal:hh:mm tt} – {endLocal:hh:mm tt} IST",
                    Status = b.Status,
                    CanCancel = b.Status == AmenityBookingStatus.Confirmed && b.EndTimeUtc > nowUtc,
                    CancellationReason = b.CancellationReason,
                    ResidentName = b.ResidentUser.FullName,
                    FlatDisplay = $"{b.Flat.Building.Code}-{b.Flat.FlatNumber}"
                };
            }).ToList();

            var model = new AdminBookingsIndexViewModel
            {
                SelectedAmenityId = amenityId,
                SelectedStatus = status,
                SelectedLocalDate = parsedDate,
                Amenities = amenities,
                Bookings = mapped
            };

            return View(model);
        }

        // ==================================================================
        // GET & POST: /Amenities/Closures/{id} (Maintenance Closures)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Closures(int id, CancellationToken ct = default)
        {
            var amenity = await _context.Amenities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (amenity == null) return NotFound();

            var closures = await _context.AmenityClosures
                .AsNoTracking()
                .Where(c => c.AmenityId == id && c.EndTimeUtc > _tz.UtcNow.AddDays(-7))
                .OrderByDescending(c => c.StartTimeUtc)
                .ToListAsync(ct);

            var today = DateOnly.FromDateTime(_tz.NowLocal);
            var model = new AdminClosuresViewModel
            {
                Amenity = new AmenityItemViewModel { Id = amenity.Id, Name = amenity.Name, Location = amenity.Location },
                AmenityId = amenity.Id,
                StartLocalDate = today.AddDays(1),
                EndLocalDate = today.AddDays(1),
                Closures = closures.Select(c => new ClosureIntervalViewModel
                {
                    Id = c.Id,
                    StartLocalDisplay = _tz.ToLocalTime(c.StartTimeUtc).ToString("dd MMM yyyy, hh:mm tt"),
                    EndLocalDisplay = _tz.ToLocalTime(c.EndTimeUtc).ToString("dd MMM yyyy, hh:mm tt"),
                    Reason = c.Reason
                }).ToList()
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddClosure(AdminClosuresViewModel input, CancellationToken ct = default)
        {
            var amenity = await _context.Amenities.FirstOrDefaultAsync(a => a.Id == input.AmenityId, ct);
            if (amenity == null) return NotFound();

            var startUtc = _tz.ToUtc(input.StartLocalDate, input.StartLocalTime);
            var endUtc = _tz.ToUtc(input.EndLocalDate, input.EndLocalTime);

            if (endUtc <= startUtc)
            {
                TempData["ErrorMessage"] = "Closure end time must be after start time.";
                return RedirectToAction(nameof(Closures), new { id = input.AmenityId });
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = _tz.UtcNow;

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var closure = new AmenityClosure
            {
                AmenityId = amenity.Id,
                StartTimeUtc = startUtc,
                EndTimeUtc = endUtc,
                Reason = input.Reason.Trim(),
                CreatedByAdminId = adminId,
                CreatedAtUtc = nowUtc
            };
            _context.AmenityClosures.Add(closure);

            // Explicitly cancel any existing confirmed bookings overlapping the maintenance window
            var conflictingBookings = await _context.AmenityBookings
                .Where(b => b.AmenityId == amenity.Id &&
                            b.Status == AmenityBookingStatus.Confirmed &&
                            b.StartTimeUtc < endUtc &&
                            b.EndTimeUtc > startUtc)
                .ToListAsync(ct);

            foreach (var b in conflictingBookings)
            {
                b.Status = AmenityBookingStatus.Cancelled;
                b.CancelledAtUtc = nowUtc;
                b.CancelledByUserId = adminId;
                b.CancellationReason = $"Facility maintenance closure: {closure.Reason}";
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            TempData["SuccessMessage"] = conflictingBookings.Count > 0
                ? $"Maintenance closure scheduled. {conflictingBookings.Count} overlapping resident booking(s) were automatically cancelled with a reason notice."
                : "Maintenance closure scheduled.";

            return RedirectToAction(nameof(Closures), new { id = input.AmenityId });
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveClosure(int id, int amenityId, CancellationToken ct = default)
        {
            var closure = await _context.AmenityClosures.FirstOrDefaultAsync(c => c.Id == id && c.AmenityId == amenityId, ct);
            if (closure != null)
            {
                _context.AmenityClosures.Remove(closure);
                await _context.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = "Maintenance closure removed.";
            }
            return RedirectToAction(nameof(Closures), new { id = amenityId });
        }
    }
}