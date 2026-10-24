using System;
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
using SocietyOS.ViewModels;

namespace SocietyOS.Controllers
{
    [Authorize(Roles = "Resident,Administrator")]
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnnouncementsController> _logger;
        private const int PageSize = 10;

        public AnnouncementsController(ApplicationDbContext context, ILogger<AnnouncementsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task<ResidentProfile?> GetVerifiedResidentProfileAsync(CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return null;

            return await _context.ResidentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == currentUserId && r.IsVerified, ct);
        }

        // ==================================================================
        // GET: /Announcements (Resident Notice Board Feed)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            AnnouncementCategory? category,
            AnnouncementPriority? priority,
            int page = 1,
            CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            if (!isAdmin)
            {
                var resident = await GetVerifiedResidentProfileAsync(ct);
                if (resident == null)
                {
                    return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
                }
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var nowUtc = DateTime.UtcNow;
            page = Math.Max(1, page);

            // Base eligibility rule: strictly Published and not expired
            var eligibleQuery = _context.Announcements
                .AsNoTracking()
                .Where(a => a.State == AnnouncementState.Published &&
                            (!a.ExpiresAtUtc.HasValue || a.ExpiresAtUtc.Value > nowUtc));

            // Calculate total unread count across all eligible published announcements for this user
            var unreadCount = await eligibleQuery
                .CountAsync(a => !a.Reads.Any(r => r.UserId == currentUserId), ct);

            var filteredQuery = eligibleQuery;
            if (category.HasValue)
            {
                filteredQuery = filteredQuery.Where(a => a.Category == category.Value);
            }

            if (priority.HasValue)
            {
                filteredQuery = filteredQuery.Where(a => a.Priority == priority.Value);
            }

            var totalCount = await filteredQuery.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            page = Math.Min(page, totalPages);

            // Intentional sort order: Pinned first -> Priority (Urgent > Important > Normal) -> PublishedAtUtc newest first
            var items = await filteredQuery
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.Priority)
                .ThenByDescending(a => a.PublishedAtUtc ?? a.CreatedAtUtc)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(a => new AnnouncementCardViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    ContentSnippet = a.Content.Length > 160
                        ? a.Content.Substring(0, 160) + "..."
                        : a.Content,
                    Category = a.Category,
                    Priority = a.Priority,
                    State = a.State,
                    IsPinned = a.IsPinned,
                    IsRead = a.Reads.Any(r => r.UserId == currentUserId),
                    CreatedAtUtc = a.CreatedAtUtc,
                    UpdatedAtUtc = a.UpdatedAtUtc,
                    PublishedAtUtc = a.PublishedAtUtc,
                    ExpiresAtUtc = a.ExpiresAtUtc
                })
                .ToListAsync(ct);

            var model = new AnnouncementFeedViewModel
            {
                SelectedCategory = category,
                SelectedPriority = priority,
                UnreadCount = unreadCount,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalAnnouncements = totalCount,
                Announcements = items
            };

            return View(model);
        }

        // ==================================================================
        // GET: /Announcements/Details/{id}
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            ResidentProfile? resident = null;

            if (!isAdmin)
            {
                resident = await GetVerifiedResidentProfileAsync(ct);
                if (resident == null)
                {
                    return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
                }
            }

            var nowUtc = DateTime.UtcNow;
            var announcement = await _context.Announcements
                .AsNoTracking()
                .Include(a => a.CreatedByAdmin)
                .Include(a => a.LastEditedByAdmin)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (announcement == null) return NotFound();

            var isEligibleForResident = announcement.State == AnnouncementState.Published &&
                                        (!announcement.ExpiresAtUtc.HasValue || announcement.ExpiresAtUtc.Value > nowUtc);

            // Non-admin residents cannot access drafts, archived items, or expired notices by guessing IDs
            if (!isAdmin && !isEligibleForResident)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            AnnouncementRead? existingRead = await _context.AnnouncementReads
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.AnnouncementId == id && r.UserId == currentUserId, ct);

            // Mark as read ONLY when an eligible published announcement is viewed (never on admin draft previews)
            if (existingRead == null && isEligibleForResident && (resident != null || isAdmin))
            {
                try
                {
                    var newRead = new AnnouncementRead
                    {
                        AnnouncementId = id,
                        UserId = currentUserId,
                        FirstReadAtUtc = nowUtc
                    };
                    _context.AnnouncementReads.Add(newRead);
                    await _context.SaveChangesAsync(ct);
                    existingRead = newRead;
                }
                catch (DbUpdateException ex)
                {
                    // Idempotent race-condition safety if opened simultaneously in two tabs
                    _logger.LogDebug(ex, "Concurrent read receipt already recorded for announcement {Id}.", id);
                    existingRead = await _context.AnnouncementReads
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.AnnouncementId == id && r.UserId == currentUserId, ct);
                }
            }

            var viewModel = new AnnouncementDetailsViewModel
            {
                Id = announcement.Id,
                Title = announcement.Title,
                Content = announcement.Content,
                Category = announcement.Category,
                Priority = announcement.Priority,
                State = announcement.State,
                IsPinned = announcement.IsPinned,
                IsRead = existingRead != null,
                FirstReadAtUtc = existingRead?.FirstReadAtUtc,
                CreatedAtUtc = announcement.CreatedAtUtc,
                UpdatedAtUtc = announcement.UpdatedAtUtc,
                PublishedAtUtc = announcement.PublishedAtUtc,
                ExpiresAtUtc = announcement.ExpiresAtUtc,
                IsAdmin = isAdmin
            };

            if (isAdmin)
            {
                viewModel.CreatedByAdminName = announcement.CreatedByAdmin?.FullName ?? "Administrator";
                viewModel.LastEditedByAdminName = announcement.LastEditedByAdmin?.FullName;
                viewModel.TotalResidentReads = await _context.AnnouncementReads
                    .CountAsync(r => r.AnnouncementId == id, ct);
            }

            return View(viewModel);
        }

        // ==================================================================
        // GET: /Announcements/Admin (Administrator Management Console)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Admin(
            AnnouncementState? state,
            AnnouncementCategory? category,
            string? q,
            CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;
            var baseQuery = _context.Announcements.AsNoTracking();

            var publishedActiveCount = await baseQuery.CountAsync(
                a => a.State == AnnouncementState.Published && (!a.ExpiresAtUtc.HasValue || a.ExpiresAtUtc.Value > nowUtc), ct);
            var draftCount = await baseQuery.CountAsync(a => a.State == AnnouncementState.Draft, ct);
            var archivedCount = await baseQuery.CountAsync(a => a.State == AnnouncementState.Archived, ct);

            var query = baseQuery;
            if (state.HasValue)
            {
                query = query.Where(a => a.State == state.Value);
            }

            if (category.HasValue)
            {
                query = query.Where(a => a.Category == category.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(a => a.Title.Contains(term) || a.Content.Contains(term));
            }

            var items = await query
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.UpdatedAtUtc)
                .Select(a => new AnnouncementCardViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    ContentSnippet = a.Content.Length > 130 ? a.Content.Substring(0, 130) + "..." : a.Content,
                    Category = a.Category,
                    Priority = a.Priority,
                    State = a.State,
                    IsPinned = a.IsPinned,
                    IsExpired = a.ExpiresAtUtc.HasValue && a.ExpiresAtUtc.Value <= nowUtc,
                    CreatedAtUtc = a.CreatedAtUtc,
                    UpdatedAtUtc = a.UpdatedAtUtc,
                    PublishedAtUtc = a.PublishedAtUtc,
                    ExpiresAtUtc = a.ExpiresAtUtc,
                    ReadCount = a.Reads.Count()
                })
                .ToListAsync(ct);

            var model = new AdminAnnouncementIndexViewModel
            {
                SelectedState = state,
                SelectedCategory = category,
                SearchQuery = q,
                PublishedActiveCount = publishedActiveCount,
                DraftCount = draftCount,
                ArchivedCount = archivedCount,
                Announcements = items
            };

            return View(model);
        }

        // ==================================================================
        // GET & POST: /Announcements/Create
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AnnouncementFormViewModel());
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AnnouncementFormViewModel model, CancellationToken ct = default)
        {
            if (model.ExpiresAtUtc.HasValue && model.ExpiresAtUtc.Value <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(model.ExpiresAtUtc), "Expiration time must be in the future.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = DateTime.UtcNow;
            var publishNow = string.Equals(model.SubmitAction, "Publish", StringComparison.OrdinalIgnoreCase);

            var announcement = new Announcement
            {
                Title = model.Title.Trim(),
                Content = model.Content.Trim(),
                Category = model.Category,
                Priority = model.Priority,
                IsPinned = model.IsPinned,
                State = publishNow ? AnnouncementState.Published : AnnouncementState.Draft,
                CreatedByAdminId = adminId,
                LastEditedByAdminId = adminId,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                PublishedAtUtc = publishNow ? nowUtc : null,
                ExpiresAtUtc = model.ExpiresAtUtc
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = publishNow
                ? "Announcement published to the community notice board."
                : "Announcement saved as a draft.";

            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // GET & POST: /Announcements/Edit/{id}
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
        {
            var announcement = await _context.Announcements.FindAsync(new object[] { id }, ct);
            if (announcement == null) return NotFound();

            var model = new AnnouncementFormViewModel
            {
                Id = announcement.Id,
                Title = announcement.Title,
                Content = announcement.Content,
                Category = announcement.Category,
                Priority = announcement.Priority,
                IsPinned = announcement.IsPinned,
                ExpiresAtUtc = announcement.ExpiresAtUtc,
                CurrentState = announcement.State
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AnnouncementFormViewModel model, CancellationToken ct = default)
        {
            if (id != model.Id) return BadRequest();

            if (model.ExpiresAtUtc.HasValue && model.ExpiresAtUtc.Value <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(model.ExpiresAtUtc), "Expiration time must be in the future.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (announcement == null) return NotFound();

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = DateTime.UtcNow;

            announcement.Title = model.Title.Trim();
            announcement.Content = model.Content.Trim();
            announcement.Category = model.Category;
            announcement.Priority = model.Priority;
            announcement.IsPinned = model.IsPinned;
            announcement.ExpiresAtUtc = model.ExpiresAtUtc;
            announcement.LastEditedByAdminId = adminId;
            announcement.UpdatedAtUtc = nowUtc;

            // Support optional "Save & Publish" from Edit view if currently a Draft
            if (announcement.State == AnnouncementState.Draft &&
                string.Equals(model.SubmitAction, "Publish", StringComparison.OrdinalIgnoreCase))
            {
                announcement.State = AnnouncementState.Published;
                announcement.PublishedAtUtc ??= nowUtc;
            }

            try
            {
                await _context.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = "Announcement updated.";
                return RedirectToAction(nameof(Details), new { id = announcement.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "This announcement was modified by another administrator. Please reload and try again.";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        // ==================================================================
        // POST: /Announcements/ChangeState (Publish, Unpublish, Archive, Restore)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeState(int id, string targetAction, CancellationToken ct = default)
        {
            var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (announcement == null) return NotFound();

            var nowUtc = DateTime.UtcNow;
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            switch (targetAction)
            {
                case "Publish":
                    if (announcement.ExpiresAtUtc.HasValue && announcement.ExpiresAtUtc.Value <= nowUtc)
                    {
                        TempData["ErrorMessage"] = "Cannot publish an announcement with a past expiration date. Edit the expiration date first.";
                        return RedirectToAction(nameof(Admin));
                    }
                    announcement.State = AnnouncementState.Published;
                    announcement.PublishedAtUtc ??= nowUtc;
                    TempData["SuccessMessage"] = "Announcement published.";
                    break;

                case "Unpublish":
                    announcement.State = AnnouncementState.Draft;
                    announcement.IsPinned = false;
                    TempData["SuccessMessage"] = "Announcement unpublished and moved to drafts.";
                    break;

                case "Archive":
                    announcement.State = AnnouncementState.Archived;
                    announcement.IsPinned = false;
                    TempData["SuccessMessage"] = "Announcement archived.";
                    break;

                case "Restore":
                    announcement.State = AnnouncementState.Draft;
                    TempData["SuccessMessage"] = "Archived announcement restored to drafts for editing.";
                    break;

                default:
                    return BadRequest("Unsupported lifecycle action.");
            }

            announcement.LastEditedByAdminId = adminId;
            announcement.UpdatedAtUtc = nowUtc;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "State change conflicted with another update. Please try again.";
            }

            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // POST: /Announcements/TogglePin
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePin(int id, CancellationToken ct = default)
        {
            var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (announcement == null) return NotFound();

            announcement.IsPinned = !announcement.IsPinned;
            announcement.LastEditedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            announcement.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = announcement.IsPinned
                ? "Announcement pinned to the top of the resident notice board."
                : "Announcement unpinned.";

            return RedirectToAction(nameof(Admin));
        }
    }
}