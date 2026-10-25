using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocietyOS.Data;
using SocietyOS.Models;
using SocietyOS.ViewModels;

namespace SocietyOS.Controllers
{
    [Authorize(Roles = "Resident,Administrator")]
    public class DailyHelpController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 10;

        public DailyHelpController(ApplicationDbContext context)
        {
            _context = context;
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

        public static string FormatCategoryLabel(DailyHelpCategory category) => category switch
        {
            DailyHelpCategory.MaidHousekeeping => "Maid / Housekeeping",
            DailyHelpCategory.Cook => "Cook / Culinary",
            DailyHelpCategory.Driver => "Driver",
            DailyHelpCategory.BabysitterChildcare => "Babysitter / Childcare",
            DailyHelpCategory.ElderCare => "Elder-Care Assistance",
            DailyHelpCategory.Gardener => "Gardener",
            DailyHelpCategory.LaundryIroning => "Laundry / Ironing",
            _ => "Other Household Service"
        };

        // ==================================================================
        // GET: /DailyHelp (Resident Household-Services Directory)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            DailyHelpCategory? category,
            string? q,
            bool savedOnly = false,
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
            page = Math.Max(1, page);

            // Resident feed strictly returns Active listings only
            var activeQuery = _context.DailyHelpListings
                .AsNoTracking()
                .Where(d => d.Status == DailyHelpListingStatus.Active);

            var savedCount = await activeQuery
                .CountAsync(d => d.Shortlists.Any(s => s.UserId == currentUserId), ct);

            var filtered = activeQuery;
            if (category.HasValue)
            {
                filtered = filtered.Where(d => d.Category == category.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                filtered = filtered.Where(d =>
                    d.ProviderName.Contains(term) ||
                    d.ShortDescription.Contains(term) ||
                    (d.ServiceArea != null && d.ServiceArea.Contains(term)));
            }

            if (savedOnly)
            {
                filtered = filtered.Where(d => d.Shortlists.Any(s => s.UserId == currentUserId));
            }

            var totalListings = await filtered.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalListings / (double)PageSize));
            page = Math.Min(page, totalPages);

            // Notice: ContactPhone is NEVER projected into feed cards
            var rawItems = await filtered
                .OrderByDescending(d => d.VerificationStatus)
                .ThenBy(d => d.ProviderName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(d => new
                {
                    d.Id,
                    d.ProviderName,
                    d.Category,
                    d.ShortDescription,
                    d.ServiceArea,
                    d.AvailabilityNotes,
                    d.VerificationStatus,
                    d.Source,
                    HasContactPhone = d.ContactPhone != null && d.ContactPhone != "",
                    IsSaved = d.Shortlists.Any(s => s.UserId == currentUserId)
                })
                .ToListAsync(ct);

            var cards = rawItems.Select(d => new DailyHelpCardViewModel
            {
                Id = d.Id,
                ProviderName = d.ProviderName,
                Category = d.Category,
                CategoryDisplay = FormatCategoryLabel(d.Category),
                ShortDescription = d.ShortDescription,
                ServiceArea = d.ServiceArea,
                AvailabilityNotes = d.AvailabilityNotes,
                VerificationStatus = d.VerificationStatus,
                Source = d.Source,
                HasContactPhone = d.HasContactPhone,
                IsSavedByCurrentUser = d.IsSaved
            }).ToList();

            var model = new DailyHelpDirectoryViewModel
            {
                SelectedCategory = category,
                SearchQuery = q,
                SavedOnly = savedOnly,
                SavedCount = savedCount,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalListings = totalListings,
                Listings = cards
            };

            return View(model);
        }

        // ==================================================================
        // GET: /DailyHelp/Details/{id}
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
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

            var listing = await _context.DailyHelpListings
                .AsNoTracking()
                .Include(d => d.VerifiedByAdmin)
                .Include(d => d.SubmittedByResidentUser)
                .FirstOrDefaultAsync(d => d.Id == id, ct);

            if (listing == null) return NotFound();

            // Non-admin users can only access Active listings
            if (!isAdmin && listing.Status != DailyHelpListingStatus.Active)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var isSaved = await _context.DailyHelpShortlists
                .AsNoTracking()
                .AnyAsync(s => s.ListingId == id && s.UserId == currentUserId, ct);

            // Contact number is revealed only if TempData["RevealContactForId"] matches this listing ID
            var revealedId = TempData["RevealContactForId"] as int?;
            var shouldRevealContact = (revealedId == id || isAdmin) && !string.IsNullOrWhiteSpace(listing.ContactPhone);

            var model = new DailyHelpDetailsViewModel
            {
                Id = listing.Id,
                ProviderName = listing.ProviderName,
                Category = listing.Category,
                CategoryDisplay = FormatCategoryLabel(listing.Category),
                ShortDescription = listing.ShortDescription,
                ServiceArea = listing.ServiceArea,
                AvailabilityNotes = listing.AvailabilityNotes,
                ExperienceNotes = listing.ExperienceNotes,
                Status = listing.Status,
                Source = listing.Source,
                VerificationStatus = listing.VerificationStatus,
                VerifiedAtUtc = listing.VerifiedAtUtc,
                HasContactPhone = !string.IsNullOrWhiteSpace(listing.ContactPhone),
                IsContactRevealed = shouldRevealContact,
                RevealedContactPhone = shouldRevealContact ? listing.ContactPhone : null,
                IsSavedByCurrentUser = isSaved,
                IsAdmin = isAdmin
            };

            if (isAdmin)
            {
                model.AdminInternalNotes = listing.AdminInternalNotes;
                model.VerifiedByAdminName = listing.VerifiedByAdmin?.FullName;
                model.RecommenderInternalDisplay = listing.SubmittedByResidentUser != null
                    ? $"{listing.SubmittedByResidentUser.FullName} ({listing.SubmittedByResidentUser.Email})"
                    : "Society Office";
            }

            return View(model);
        }

        // ==================================================================
        // POST: /DailyHelp/RevealContact/{id} (Intentional Contact Access)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevealContact(int id, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            if (!isAdmin)
            {
                var resident = await GetVerifiedResidentProfileAsync(ct);
                if (resident == null) return Forbid();
            }

            var existsAndActive = await _context.DailyHelpListings
                .AsNoTracking()
                .AnyAsync(d => d.Id == id && (d.Status == DailyHelpListingStatus.Active || isAdmin), ct);

            if (!existsAndActive) return NotFound();

            TempData["RevealContactForId"] = id;
            return RedirectToAction(nameof(Details), new { id });
        }

        // ==================================================================
        // POST: /DailyHelp/ToggleShortlist
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleShortlist(int id, string? returnUrl, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            if (!isAdmin)
            {
                var resident = await GetVerifiedResidentProfileAsync(ct);
                if (resident == null) return Forbid();
            }

            var listingExists = await _context.DailyHelpListings
                .AsNoTracking()
                .AnyAsync(d => d.Id == id && d.Status == DailyHelpListingStatus.Active, ct);

            if (!listingExists) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var existing = await _context.DailyHelpShortlists
                .FirstOrDefaultAsync(s => s.ListingId == id && s.UserId == currentUserId, ct);

            try
            {
                if (existing == null)
                {
                    _context.DailyHelpShortlists.Add(new DailyHelpShortlist
                    {
                        ListingId = id,
                        UserId = currentUserId,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    _context.DailyHelpShortlists.Remove(existing);
                }
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Safely absorb concurrent duplicate shortlist click
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ==================================================================
        // GET & POST: /DailyHelp/Recommend (Resident Recommendation Submission)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Recommend(CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null && !User.IsInRole("Administrator"))
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            return View(new ResidentRecommendDailyHelpViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recommend(ResidentRecommendDailyHelpViewModel model, CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = DateTime.UtcNow;

            var listing = new DailyHelpListing
            {
                ProviderName = model.ProviderName.Trim(),
                Category = model.Category,
                ShortDescription = model.ShortDescription.Trim(),
                ServiceArea = model.ServiceArea?.Trim(),
                AvailabilityNotes = model.AvailabilityNotes?.Trim(),
                ExperienceNotes = model.ExperienceNotes?.Trim(),
                ContactPhone = model.ContactPhone.Trim(),
                Status = DailyHelpListingStatus.PendingApproval, // Hidden until admin moderation
                Source = DailyHelpListingSource.ResidentRecommended,
                VerificationStatus = DailyHelpVerificationStatus.SocietyListed,
                SubmittedByResidentUserId = currentUserId,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _context.DailyHelpListings.Add(listing);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = $"Thank you! Your recommendation for {listing.ProviderName} has been submitted for society office review.";
            return RedirectToAction(nameof(Index));
        }

        // ==================================================================
        // GET: /DailyHelp/Admin (Administrator Directory & Moderation Console)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Admin(
            DailyHelpListingStatus? status,
            DailyHelpCategory? category,
            string? q,
            CancellationToken ct = default)
        {
            var baseQuery = _context.DailyHelpListings
                .AsNoTracking()
                .Include(d => d.SubmittedByResidentUser);

            var activeCount = await baseQuery.CountAsync(d => d.Status == DailyHelpListingStatus.Active, ct);
            var pendingCount = await baseQuery.CountAsync(d => d.Status == DailyHelpListingStatus.PendingApproval, ct);
            var inactiveCount = await baseQuery.CountAsync(d => d.Status == DailyHelpListingStatus.Inactive || d.Status == DailyHelpListingStatus.Rejected, ct);

            var query = baseQuery.AsQueryable();
            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status.Value);
            }

            if (category.HasValue)
            {
                query = query.Where(d => d.Category == category.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(d => d.ProviderName.Contains(term) || d.ShortDescription.Contains(term));
            }

            var rawItems = await query
                .OrderBy(d => d.Status == DailyHelpListingStatus.PendingApproval ? 0 : 1)
                .ThenByDescending(d => d.UpdatedAtUtc)
                .ToListAsync(ct);

            var cards = rawItems.Select(d => new DailyHelpCardViewModel
            {
                Id = d.Id,
                ProviderName = d.ProviderName,
                Category = d.Category,
                CategoryDisplay = FormatCategoryLabel(d.Category),
                ShortDescription = d.ShortDescription,
                ServiceArea = d.ServiceArea,
                AvailabilityNotes = d.AvailabilityNotes,
                VerificationStatus = d.VerificationStatus,
                Source = d.Source,
                Status = d.Status,
                HasContactPhone = !string.IsNullOrWhiteSpace(d.ContactPhone),
                AdminInternalNotes = d.AdminInternalNotes,
                RecommenderDisplay = d.SubmittedByResidentUser != null
                    ? d.SubmittedByResidentUser.FullName
                    : "Society Office",
                UpdatedAtUtc = d.UpdatedAtUtc
            }).ToList();

            var model = new AdminDailyHelpIndexViewModel
            {
                SelectedStatus = status,
                SelectedCategory = category,
                SearchQuery = q,
                ActiveCount = activeCount,
                PendingCount = pendingCount,
                InactiveCount = inactiveCount,
                Listings = cards
            };

            return View(model);
        }

        // ==================================================================
        // GET & POST: /DailyHelp/Create (Admin Create Listing)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AdminDailyHelpFormViewModel());
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminDailyHelpFormViewModel model, CancellationToken ct = default)
        {
            if (!ModelState.IsValid) return View(model);

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = DateTime.UtcNow;

            var listing = new DailyHelpListing
            {
                ProviderName = model.ProviderName.Trim(),
                Category = model.Category,
                ShortDescription = model.ShortDescription.Trim(),
                ServiceArea = model.ServiceArea?.Trim(),
                AvailabilityNotes = model.AvailabilityNotes?.Trim(),
                ExperienceNotes = model.ExperienceNotes?.Trim(),
                ContactPhone = model.ContactPhone?.Trim(),
                Status = model.Status,
                Source = DailyHelpListingSource.SocietyManaged,
                VerificationStatus = model.VerificationStatus,
                VerifiedByAdminId = model.VerificationStatus == DailyHelpVerificationStatus.GatePassChecked ? adminId : null,
                VerifiedAtUtc = model.VerificationStatus == DailyHelpVerificationStatus.GatePassChecked ? nowUtc : null,
                AdminInternalNotes = model.AdminInternalNotes?.Trim(),
                LastModifiedByAdminId = adminId,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _context.DailyHelpListings.Add(listing);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = $"Provider listing '{listing.ProviderName}' created.";
            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // GET & POST: /DailyHelp/Edit/{id} (Admin Edit Listing)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
        {
            var d = await _context.DailyHelpListings.FindAsync(new object[] { id }, ct);
            if (d == null) return NotFound();

            var model = new AdminDailyHelpFormViewModel
            {
                Id = d.Id,
                ProviderName = d.ProviderName,
                Category = d.Category,
                ShortDescription = d.ShortDescription,
                ServiceArea = d.ServiceArea,
                AvailabilityNotes = d.AvailabilityNotes,
                ExperienceNotes = d.ExperienceNotes,
                ContactPhone = d.ContactPhone,
                Status = d.Status,
                VerificationStatus = d.VerificationStatus,
                AdminInternalNotes = d.AdminInternalNotes
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminDailyHelpFormViewModel model, CancellationToken ct = default)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);

            var listing = await _context.DailyHelpListings.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (listing == null) return NotFound();

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = DateTime.UtcNow;

            listing.ProviderName = model.ProviderName.Trim();
            listing.Category = model.Category;
            listing.ShortDescription = model.ShortDescription.Trim();
            listing.ServiceArea = model.ServiceArea?.Trim();
            listing.AvailabilityNotes = model.AvailabilityNotes?.Trim();
            listing.ExperienceNotes = model.ExperienceNotes?.Trim();
            listing.ContactPhone = model.ContactPhone?.Trim();
            listing.Status = model.Status;

            if (listing.VerificationStatus != model.VerificationStatus)
            {
                listing.VerificationStatus = model.VerificationStatus;
                listing.VerifiedByAdminId = model.VerificationStatus == DailyHelpVerificationStatus.GatePassChecked ? adminId : null;
                listing.VerifiedAtUtc = model.VerificationStatus == DailyHelpVerificationStatus.GatePassChecked ? nowUtc : null;
            }

            listing.AdminInternalNotes = model.AdminInternalNotes?.Trim();
            listing.LastModifiedByAdminId = adminId;
            listing.UpdatedAtUtc = nowUtc;

            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = $"Listing for '{listing.ProviderName}' updated.";
            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // POST: /DailyHelp/ChangeStatus (Approve, Reject, Activate, Deactivate)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, DailyHelpListingStatus newStatus, CancellationToken ct = default)
        {
            var listing = await _context.DailyHelpListings.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (listing == null) return NotFound();

            listing.Status = newStatus;
            listing.LastModifiedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            listing.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = $"Status for '{listing.ProviderName}' changed to {newStatus}.";
            return RedirectToAction(nameof(Admin));
        }
    }
}