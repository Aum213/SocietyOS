using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocietyOS.Data;
using SocietyOS.Hubs;
using SocietyOS.Models;
using SocietyOS.Services;
using SocietyOS.ViewModels;

namespace SocietyOS.Controllers
{
    [Authorize]
    public class EmergencyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISocietyTimeZoneService _tz;
        private readonly IHubContext<EmergencySosHub> _sosHub;
        private readonly ILogger<EmergencyController> _logger;

        public EmergencyController(
            ApplicationDbContext context,
            ISocietyTimeZoneService tz,
            IHubContext<EmergencySosHub> sosHub,
            ILogger<EmergencyController> logger)
        {
            _context = context;
            _tz = tz;
            _sosHub = sosHub;
            _logger = logger;
        }

        private async Task<ResidentProfile?> GetVerifiedResidentProfileAsync(CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return null;

            return await _context.ResidentProfiles
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Flat)
                    .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(r => r.UserId == currentUserId && r.IsVerified, ct);
        }

        public static string FormatCategoryLabel(EmergencyCategory category) => category switch
        {
            EmergencyCategory.MedicalEmergency => "Medical Emergency",
            EmergencyCategory.FireOrSmoke => "Fire or Smoke",
            EmergencyCategory.SecurityOrSafety => "Security / Intrusion / Safety",
            EmergencyCategory.LiftEntrapment => "Lift / Elevator Entrapment",
            _ => "Other Urgent Emergency"
        };

        private static string FormatNotificationStatus(EmergencyNotificationStatus status) => status switch
        {
            EmergencyNotificationStatus.InAppQueueOnly =>
                "Recorded in SocietyOS Responder Queue (External SMS/Push Alert Delivery Not Configured)",
            _ => "Recorded in Responder Queue"
        };

        // ==================================================================
        // GET: /Emergency (Resident SOS Entry Point)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var isGuard = User.IsInRole("SecurityGuard");
            var isAdmin = User.IsInRole("Administrator");
            var resident = await GetVerifiedResidentProfileAsync(ct);

            if (isGuard && resident == null)
            {
                return RedirectToAction(nameof(Queue));
            }

            if (resident == null && !isAdmin)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var activeContacts = await _context.SocietyEmergencyContacts
                .AsNoTracking()
                .Where(c => c.IsActive && c.ShowOnEmergencySos)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.DisplayName)
                .Select(c => new EmergencyContactItemViewModel
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    PhoneNumber = c.PhoneNumber,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    DisplayOrder = c.DisplayOrder
                })
                .ToListAsync(ct);

            var recentIncidents = await _context.EmergencyIncidents
                .AsNoTracking()
                .Where(e => e.ReporterUserId == currentUserId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(5)
                .ToListAsync(ct);

            var model = new EmergencyLandingViewModel
            {
                ResidentName = resident?.User.FullName ?? User.Identity?.Name ?? "Resident",
                FlatDisplay = resident != null
                    ? $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}"
                    : "Admin Office",
                BuildingName = resident?.Flat.Building.Name ?? "Society Management",
                IsResponder = isGuard || isAdmin,
                ActiveIncidentCountForResident = recentIncidents.Count(e =>
                    e.Status != EmergencyStatus.Resolved && e.Status != EmergencyStatus.Cancelled),
                ActiveContacts = activeContacts,
                RecentResidentIncidents = recentIncidents.Select(e => new EmergencyIncidentCardViewModel
                {
                    Id = e.Id,
                    ReferenceCode = e.ReferenceCode,
                    Category = e.Category,
                    CategoryDisplay = FormatCategoryLabel(e.Category),
                    Status = e.Status,
                    Description = e.Description,
                    CreatedAtLocalDisplay = _tz.ToLocalTime(e.CreatedAtUtc).ToString("dd MMM yyyy, hh:mm tt") + " IST",
                    NotificationStatusDisplay = FormatNotificationStatus(e.NotificationStatus)
                }).ToList()
            };

            return View(model);
        }

        // ==================================================================
        // POST: /Emergency/Confirm (Create Verified Emergency Incident)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm([FromForm] CreateEmergencyInputModel input, CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null)
            {
                TempData["ErrorMessage"] = "Only verified residents linked to an authorized flat may create an SOS incident.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please verify the emergency details and try again.";
                return RedirectToAction(nameof(Index));
            }

            var nowUtc = _tz.UtcNow;

            var recentDuplicate = await _context.EmergencyIncidents
                .AsNoTracking()
                .Where(e => e.ReporterUserId == resident.UserId &&
                            e.Category == input.Category &&
                            e.Status != EmergencyStatus.Resolved &&
                            e.Status != EmergencyStatus.Cancelled &&
                            e.CreatedAtUtc >= nowUtc.AddSeconds(-60))
                .OrderByDescending(e => e.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (recentDuplicate != null)
            {
                TempData["WarningMessage"] = $"Your emergency incident ({recentDuplicate.ReferenceCode}) was already recorded a moment ago.";
                return RedirectToAction(nameof(Details), new { id = recentDuplicate.Id });
            }

            var randomSuffix = RandomNumberGenerator.GetInt32(1000, 9999);
            var referenceCode = $"SOS-{nowUtc:yyyyMMdd}-{randomSuffix}";

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var incident = new EmergencyIncident
            {
                ReferenceCode = referenceCode,
                ReporterUserId = resident.UserId,
                FlatId = resident.FlatId,
                BuildingId = resident.Flat.BuildingId,
                Category = input.Category,
                Description = input.Description?.Trim(),
                Status = EmergencyStatus.Open,
                NotificationStatus = EmergencyNotificationStatus.InAppQueueOnly,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _context.EmergencyIncidents.Add(incident);
            await _context.SaveChangesAsync(ct);

            _context.EmergencyStatusHistories.Add(new EmergencyStatusHistory
            {
                IncidentId = incident.Id,
                PreviousStatus = EmergencyStatus.Open,
                NewStatus = EmergencyStatus.Open,
                ActorUserId = resident.UserId,
                Note = "SOS incident created by verified resident.",
                ChangedAtUtc = nowUtc
            });

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            try
            {
                var localTimeStr = _tz.ToLocalTime(incident.CreatedAtUtc).ToString("dd MMM, hh:mm:ss tt") + " IST";
                await _sosHub.Clients.Group(EmergencySosHub.GuardsGroupName).SendAsync(
                    "NewSosIncident",
                    new
                    {
                        id = incident.Id,
                        referenceCode = incident.ReferenceCode,
                        categoryDisplay = FormatCategoryLabel(incident.Category),
                        flatDisplay = $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}",
                        buildingName = resident.Flat.Building.Name,
                        createdAtLocalDisplay = localTimeStr,
                        detailsUrl = Url.Action(nameof(Details), "Emergency", new { id = incident.Id }),
                        queueUrl = Url.Action(nameof(Queue), "Emergency")
                    },
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast real-time SignalR alert for SOS incident {Id}.", incident.Id);
            }

            TempData["SuccessMessage"] = $"Incident {incident.ReferenceCode} has been recorded in the SocietyOS Responder Queue.";
            return RedirectToAction(nameof(Details), new { id = incident.Id });
        }

        // ==================================================================
        // GET: /Emergency/Details/{id} (Strict Owner or Responder Access)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var isGuard = User.IsInRole("SecurityGuard");
            var isAdmin = User.IsInRole("Administrator");
            var isResponder = isGuard || isAdmin;

            var incident = await _context.EmergencyIncidents
                .AsNoTracking()
                .Include(e => e.ReporterUser)
                .Include(e => e.Flat)
                    .ThenInclude(f => f.Building)
                .Include(e => e.AcknowledgedByUser)
                .Include(e => e.ResponderUser)
                .Include(e => e.ResolvedByUser)
                .FirstOrDefaultAsync(e => e.Id == id, ct);

            if (incident == null) return NotFound();

            var isOwner = incident.ReporterUserId == currentUserId;

            if (!isOwner && !isResponder)
            {
                return NotFound();
            }

            var history = await _context.EmergencyStatusHistories
                .AsNoTracking()
                .Where(h => h.IncidentId == id)
                .Include(h => h.ActorUser)
                .OrderByDescending(h => h.ChangedAtUtc)
                .ToListAsync(ct);

            var activeContacts = await _context.SocietyEmergencyContacts
                .AsNoTracking()
                .Where(c => c.IsActive && c.ShowOnEmergencySos)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new EmergencyContactItemViewModel
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    PhoneNumber = c.PhoneNumber,
                    Description = c.Description
                })
                .ToListAsync(ct);

            var model = new EmergencyIncidentDetailsViewModel
            {
                Id = incident.Id,
                ReferenceCode = incident.ReferenceCode,
                Category = incident.Category,
                CategoryDisplay = FormatCategoryLabel(incident.Category),
                Status = incident.Status,
                Description = incident.Description,
                FlatDisplay = $"{incident.Flat.Building.Code}-{incident.Flat.FlatNumber}",
                BuildingName = incident.Flat.Building.Name,
                ResidentName = incident.ReporterUser.FullName,
                NotificationStatusDisplay = FormatNotificationStatus(incident.NotificationStatus),
                CreatedAtLocalDisplay = _tz.ToLocalTime(incident.CreatedAtUtc).ToString("dd MMM yyyy, hh:mm:ss tt") + " IST",
                AcknowledgedAtLocalDisplay = incident.AcknowledgedAtUtc.HasValue
                    ? _tz.ToLocalTime(incident.AcknowledgedAtUtc.Value).ToString("dd MMM, hh:mm tt") + " IST"
                    : null,
                AcknowledgedByName = incident.AcknowledgedByUser?.FullName,
                RespondingAtLocalDisplay = incident.RespondingAtUtc.HasValue
                    ? _tz.ToLocalTime(incident.RespondingAtUtc.Value).ToString("dd MMM, hh:mm tt") + " IST"
                    : null,
                ResponderName = incident.ResponderUser?.FullName,
                ResolvedAtLocalDisplay = incident.ResolvedAtUtc.HasValue
                    ? _tz.ToLocalTime(incident.ResolvedAtUtc.Value).ToString("dd MMM, hh:mm tt") + " IST"
                    : null,
                ResolvedByName = incident.ResolvedByUser?.FullName,
                ResolutionNote = incident.ResolutionNote,
                CancelledAtLocalDisplay = incident.CancelledAtUtc.HasValue
                    ? _tz.ToLocalTime(incident.CancelledAtUtc.Value).ToString("dd MMM, hh:mm tt") + " IST"
                    : null,
                CancellationReason = incident.CancellationReason,
                IsOwnerResident = isOwner,
                IsResponder = isResponder,
                IsAdmin = isAdmin,
                ActiveContacts = activeContacts,
                History = history.Select(h => new EmergencyHistoryItemViewModel
                {
                    PreviousStatus = h.PreviousStatus,
                    NewStatus = h.NewStatus,
                    ActorName = h.ActorUser.FullName,
                    Note = h.Note,
                    ChangedAtLocalDisplay = _tz.ToLocalTime(h.ChangedAtUtc).ToString("dd MMM, hh:mm:ss tt") + " IST"
                }).ToList()
            };

            return View(model);
        }

        // ==================================================================
        // GET: /Emergency/MyIncidents (Resident's Personal SOS History)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> MyIncidents(CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null && !User.IsInRole("Administrator"))
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var records = await _context.EmergencyIncidents
                .AsNoTracking()
                .Where(e => e.ReporterUserId == currentUserId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .ToListAsync(ct);

            var list = records.Select(e => new EmergencyIncidentCardViewModel
            {
                Id = e.Id,
                ReferenceCode = e.ReferenceCode,
                Category = e.Category,
                CategoryDisplay = FormatCategoryLabel(e.Category),
                Status = e.Status,
                Description = e.Description,
                CreatedAtLocalDisplay = _tz.ToLocalTime(e.CreatedAtUtc).ToString("dd MMM yyyy, hh:mm tt") + " IST",
                NotificationStatusDisplay = FormatNotificationStatus(e.NotificationStatus)
            }).ToList();

            return View(list);
        }

        // ==================================================================
        // POST: /Emergency/CancelOwn (Resident Cancels False Alarm if Open)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOwn(int id, string? reason, CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var incident = await _context.EmergencyIncidents
                .FirstOrDefaultAsync(e => e.Id == id && e.ReporterUserId == currentUserId, ct);

            if (incident == null) return NotFound();

            if (incident.Status != EmergencyStatus.Open)
            {
                TempData["WarningMessage"] = "Only Open unacknowledged incidents can be cancelled directly by the resident. Contact the security desk if resolved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var nowUtc = _tz.UtcNow;
            var prev = incident.Status;

            incident.Status = EmergencyStatus.Cancelled;
            incident.CancelledAtUtc = nowUtc;
            incident.CancelledByUserId = currentUserId;
            incident.CancellationReason = string.IsNullOrWhiteSpace(reason)
                ? "Cancelled by reporting resident (False alarm / Resolved)"
                : reason.Trim();
            incident.UpdatedAtUtc = nowUtc;

            _context.EmergencyStatusHistories.Add(new EmergencyStatusHistory
            {
                IncidentId = incident.Id,
                PreviousStatus = prev,
                NewStatus = EmergencyStatus.Cancelled,
                ActorUserId = currentUserId,
                Note = incident.CancellationReason,
                ChangedAtUtc = nowUtc
            });

            try
            {
                await _context.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = $"Incident {incident.ReferenceCode} has been marked as Cancelled.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "A responder updated this incident at the same time. Please review the current status.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ==================================================================
        // GET: /Emergency/Queue (Security Guard & Admin Responder Queue)
        // ==================================================================
        [Authorize(Roles = "SecurityGuard,Administrator")]
        [HttpGet]
        public async Task<IActionResult> Queue(
            EmergencyStatus? status,
            EmergencyCategory? category,
            CancellationToken ct = default)
        {
            var baseQuery = _context.EmergencyIncidents
                .AsNoTracking()
                .Include(e => e.Flat)
                    .ThenInclude(f => f.Building)
                .Include(e => e.ReporterUser);

            var openCount = await baseQuery.CountAsync(e => e.Status == EmergencyStatus.Open, ct);
            var ackCount = await baseQuery.CountAsync(e => e.Status == EmergencyStatus.Acknowledged, ct);
            var respCount = await baseQuery.CountAsync(e => e.Status == EmergencyStatus.Responding, ct);
            var resCount = await baseQuery.CountAsync(e => e.Status == EmergencyStatus.Resolved, ct);

            var query = baseQuery.AsQueryable();
            if (status.HasValue)
            {
                query = query.Where(e => e.Status == status.Value);
            }
            if (category.HasValue)
            {
                query = query.Where(e => e.Category == category.Value);
            }

            var raw = await query
                .OrderBy(e => e.Status == EmergencyStatus.Open ? 0 :
                              e.Status == EmergencyStatus.Acknowledged ? 1 :
                              e.Status == EmergencyStatus.Responding ? 2 :
                              e.Status == EmergencyStatus.Resolved ? 3 : 4)
                .ThenByDescending(e => e.CreatedAtUtc)
                .Take(100)
                .ToListAsync(ct);

            var model = new ResponderQueueViewModel
            {
                SelectedStatus = status,
                SelectedCategory = category,
                OpenCount = openCount,
                AcknowledgedCount = ackCount,
                RespondingCount = respCount,
                ResolvedCount = resCount,
                Incidents = raw.Select(e => new EmergencyIncidentCardViewModel
                {
                    Id = e.Id,
                    ReferenceCode = e.ReferenceCode,
                    Category = e.Category,
                    CategoryDisplay = FormatCategoryLabel(e.Category),
                    Status = e.Status,
                    Description = e.Description,
                    CreatedAtLocalDisplay = _tz.ToLocalTime(e.CreatedAtUtc).ToString("dd MMM, hh:mm:ss tt") + " IST",
                    FlatDisplay = $"{e.Flat.Building.Code}-{e.Flat.FlatNumber}",
                    BuildingName = e.Flat.Building.Name,
                    ResidentName = e.ReporterUser.FullName
                }).ToList()
            };

            return View(model);
        }

        // ==================================================================
        // POST: /Emergency/UpdateStatus (Responder Lifecycle Transition)
        // ==================================================================
        [Authorize(Roles = "SecurityGuard,Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromForm] ResponderStatusUpdateInputModel input, CancellationToken ct = default)
        {
            var incident = await _context.EmergencyIncidents.FirstOrDefaultAsync(e => e.Id == input.IncidentId, ct);
            if (incident == null) return NotFound();

            var prev = incident.Status;
            var next = input.NewStatus;

            if (prev == next)
            {
                TempData["WarningMessage"] = $"Incident is already in {next} status.";
                return RedirectToAction(nameof(Details), new { id = incident.Id });
            }

            bool isAdmin = User.IsInRole("Administrator");
            bool isValid = (prev, next) switch
            {
                (EmergencyStatus.Open, EmergencyStatus.Acknowledged) => true,
                (EmergencyStatus.Open, EmergencyStatus.Responding) => true,
                (EmergencyStatus.Acknowledged, EmergencyStatus.Responding) => true,
                (EmergencyStatus.Acknowledged, EmergencyStatus.Resolved) => true,
                (EmergencyStatus.Responding, EmergencyStatus.Resolved) => true,
                (EmergencyStatus.Open, EmergencyStatus.Cancelled) when isAdmin => true,
                (EmergencyStatus.Acknowledged, EmergencyStatus.Cancelled) when isAdmin => true,
                _ => false
            };

            if (!isValid)
            {
                TempData["ErrorMessage"] = $"Invalid status transition from {prev} to {next}.";
                return RedirectToAction(nameof(Details), new { id = incident.Id });
            }

            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = _tz.UtcNow;

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                incident.Status = next;
                incident.UpdatedAtUtc = nowUtc;

                if (next == EmergencyStatus.Acknowledged)
                {
                    incident.AcknowledgedAtUtc ??= nowUtc;
                    incident.AcknowledgedByUserId ??= actorId;
                }
                else if (next == EmergencyStatus.Responding)
                {
                    incident.AcknowledgedAtUtc ??= nowUtc;
                    incident.AcknowledgedByUserId ??= actorId;
                    incident.RespondingAtUtc = nowUtc;
                    incident.ResponderUserId = actorId;
                }
                else if (next == EmergencyStatus.Resolved)
                {
                    incident.ResolvedAtUtc = nowUtc;
                    incident.ResolvedByUserId = actorId;
                    if (!string.IsNullOrWhiteSpace(input.Note))
                    {
                        incident.ResolutionNote = input.Note.Trim();
                    }
                }
                else if (next == EmergencyStatus.Cancelled)
                {
                    incident.CancelledAtUtc = nowUtc;
                    incident.CancelledByUserId = actorId;
                    incident.CancellationReason = input.Note?.Trim() ?? "Cancelled by Administrator";
                }

                _context.EmergencyStatusHistories.Add(new EmergencyStatusHistory
                {
                    IncidentId = incident.Id,
                    PreviousStatus = prev,
                    NewStatus = next,
                    ActorUserId = actorId,
                    Note = input.Note?.Trim(),
                    ChangedAtUtc = nowUtc
                });

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["SuccessMessage"] = $"Incident {incident.ReferenceCode} updated to {next}.";
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                TempData["ErrorMessage"] = "Another responder updated this incident simultaneously. Please review the latest status.";
            }

            return RedirectToAction(nameof(Details), new { id = incident.Id });
        }

        // ==================================================================
        // GET & POST: /Emergency/Contacts (Admin Emergency Contact Config)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Contacts(CancellationToken ct = default)
        {
            var contacts = await _context.SocietyEmergencyContacts
                .AsNoTracking()
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.DisplayName)
                .Select(c => new EmergencyContactItemViewModel
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    PhoneNumber = c.PhoneNumber,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    DisplayOrder = c.DisplayOrder
                })
                .ToListAsync(ct);

            return View(new AdminEmergencyContactsViewModel { Contacts = contacts });
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveContact(AdminEmergencyContactsViewModel input, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                input.Contacts = await _context.SocietyEmergencyContacts
                    .AsNoTracking()
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new EmergencyContactItemViewModel
                    {
                        Id = c.Id,
                        DisplayName = c.DisplayName,
                        PhoneNumber = c.PhoneNumber,
                        Description = c.Description,
                        IsActive = c.IsActive,
                        DisplayOrder = c.DisplayOrder
                    })
                    .ToListAsync(ct);
                return View("Contacts", input);
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = _tz.UtcNow;

            var contact = new SocietyEmergencyContact
            {
                DisplayName = input.DisplayName.Trim(),
                PhoneNumber = input.PhoneNumber.Trim(),
                Description = input.Description?.Trim(),
                DisplayOrder = input.DisplayOrder,
                IsActive = input.IsActive,
                UpdatedByAdminId = adminId,
                UpdatedAtUtc = nowUtc
            };

            _context.SocietyEmergencyContacts.Add(contact);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = $"Emergency contact '{contact.DisplayName}' added.";
            return RedirectToAction(nameof(Contacts));
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleContact(int id, CancellationToken ct = default)
        {
            var contact = await _context.SocietyEmergencyContacts.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (contact != null)
            {
                contact.IsActive = !contact.IsActive;
                contact.UpdatedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                contact.UpdatedAtUtc = _tz.UtcNow;
                await _context.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = $"Contact '{contact.DisplayName}' is now {(contact.IsActive ? "Active" : "Inactive")}.";
            }
            return RedirectToAction(nameof(Contacts));
        }
    }
}