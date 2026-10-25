using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocietyOS.Data;
using SocietyOS.Models;
using SocietyOS.Services;
using SocietyOS.ViewModels;

namespace SocietyOS.Controllers
{
    [Authorize(Roles = "Resident,Administrator")]
    public class MarketplaceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMarketplacePhotoStorageService _photoStorage;
        private readonly ISocietyTimeZoneService _tz;
        private const int PageSize = 10;

        public MarketplaceController(
            ApplicationDbContext context,
            IMarketplacePhotoStorageService photoStorage,
            ISocietyTimeZoneService tz)
        {
            _context = context;
            _photoStorage = photoStorage;
            _tz = tz;
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

        public static string FormatCategoryLabel(MarketplaceCategory category) => category switch
        {
            MarketplaceCategory.FurnitureAndHome => "Furniture & Home",
            MarketplaceCategory.Electronics => "Electronics",
            MarketplaceCategory.BooksAndStudy => "Books & Study",
            MarketplaceCategory.ClothingAndAccessories => "Clothing & Accessories",
            MarketplaceCategory.SportsAndFitness => "Sports & Fitness",
            MarketplaceCategory.KidsAndToys => "Kids & Toys",
            MarketplaceCategory.KitchenAndAppliances => "Kitchen & Appliances",
            _ => "Other Items"
        };

        public static string FormatConditionLabel(MarketplaceItemCondition condition) => condition switch
        {
            MarketplaceItemCondition.NewUnopened => "New / Unopened",
            MarketplaceItemCondition.LikeNew => "Like New",
            MarketplaceItemCondition.Good => "Good",
            _ => "Fair"
        };

        public static string FormatPriceInr(MarketplaceListingType type, decimal? price)
        {
            if (type == MarketplaceListingType.FreeGiveaway || !price.HasValue || price.Value <= 0)
            {
                return "FREE (Giveaway)";
            }
            return string.Format(new CultureInfo("en-IN"), "₹{0:N0}", price.Value);
        }

        // ==================================================================
        // GET: /Marketplace (Resident Community Marketplace Feed)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            MarketplaceCategory? category,
            MarketplaceListingType? type,
            decimal? maxPrice,
            string? q,
            string sort = "recent",
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

            var myCount = await _context.MarketplaceListings
                .AsNoTracking()
                .CountAsync(m => m.SellerUserId == currentUserId, ct);

            // Public feed strictly displays Active (approved) listings only
            var query = _context.MarketplaceListings
                .AsNoTracking()
                .Include(m => m.Building)
                .Where(m => m.Status == MarketplaceListingStatus.Active);

            if (category.HasValue)
            {
                query = query.Where(m => m.Category == category.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(m => m.ListingType == type.Value);
            }

            if (maxPrice.HasValue && maxPrice.Value >= 0)
            {
                query = query.Where(m => m.ListingType == MarketplaceListingType.FreeGiveaway ||
                                         (m.PriceInr.HasValue && m.PriceInr.Value <= maxPrice.Value));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(m => m.Title.Contains(term) || m.Description.Contains(term));
            }

            var totalCount = await query.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            page = Math.Min(page, totalPages);

            query = sort switch
            {
                "price-asc" => query.OrderBy(m => m.ListingType == MarketplaceListingType.FreeGiveaway ? 0m : (m.PriceInr ?? 0m))
                                    .ThenByDescending(m => m.CreatedAtUtc),
                "price-desc" => query.OrderByDescending(m => m.ListingType == MarketplaceListingType.FreeGiveaway ? 0m : (m.PriceInr ?? 0m))
                                     .ThenByDescending(m => m.CreatedAtUtc),
                _ => query.OrderByDescending(m => m.CreatedAtUtc)
            };

            var rawItems = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.Description,
                    m.Category,
                    m.ListingType,
                    m.PriceInr,
                    m.Condition,
                    m.Status,
                    BuildingCode = m.Building.Code,
                    m.CreatedAtUtc,
                    HasImage = m.ImageStorageKey != null && m.ImageStorageKey != "",
                    IsOwn = m.SellerUserId == currentUserId
                })
                .ToListAsync(ct);

            var cards = rawItems.Select(m => new MarketplaceCardViewModel
            {
                Id = m.Id,
                Title = m.Title,
                DescriptionSnippet = m.Description.Length > 130 ? m.Description.Substring(0, 130) + "..." : m.Description,
                Category = m.Category,
                CategoryDisplay = FormatCategoryLabel(m.Category),
                ListingType = m.ListingType,
                PriceInr = m.PriceInr,
                PriceDisplay = FormatPriceInr(m.ListingType, m.PriceInr),
                Condition = m.Condition,
                ConditionDisplay = FormatConditionLabel(m.Condition),
                Status = m.Status,
                TowerDisplay = $"Tower {m.BuildingCode}",
                PostedDateDisplay = _tz.ToLocalTime(m.CreatedAtUtc).ToString("dd MMM yyyy"),
                HasImage = m.HasImage,
                IsOwnListing = m.IsOwn
            }).ToList();

            var model = new MarketplaceFeedViewModel
            {
                SelectedCategory = category,
                SelectedType = type,
                MaxPriceInr = maxPrice,
                SearchQuery = q,
                SortBy = sort,
                MyListingsCount = myCount,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalActiveListings = totalCount,
                Listings = cards
            };

            return View(model);
        }

        // ==================================================================
        // GET: /Marketplace/Details/{id}
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

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var listing = await _context.MarketplaceListings
                .AsNoTracking()
                .Include(m => m.Building)
                .Include(m => m.Flat)
                .Include(m => m.SellerUser)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            if (listing == null) return NotFound();

            var isOwner = listing.SellerUserId == currentUserId;

            // Non-owner residents cannot view PendingApproval, Sold, Withdrawn, or Rejected listings by guessing IDs
            if (!isAdmin && !isOwner && listing.Status != MarketplaceListingStatus.Active)
            {
                return NotFound();
            }

            var revealedId = TempData["RevealSellerContactForId"] as int?;
            var shouldRevealContact = (revealedId == id && listing.Status == MarketplaceListingStatus.Active) || isOwner || isAdmin;

            var model = new MarketplaceDetailsViewModel
            {
                Id = listing.Id,
                Title = listing.Title,
                Description = listing.Description,
                Category = listing.Category,
                CategoryDisplay = FormatCategoryLabel(listing.Category),
                ListingType = listing.ListingType,
                PriceInr = listing.PriceInr,
                PriceDisplay = FormatPriceInr(listing.ListingType, listing.PriceInr),
                Condition = listing.Condition,
                ConditionDisplay = FormatConditionLabel(listing.Condition),
                Status = listing.Status,
                TowerDisplay = $"Tower {listing.Building.Code} ({listing.Building.Name})",
                PostedDateDisplay = _tz.ToLocalTime(listing.CreatedAtUtc).ToString("dd MMM yyyy, hh:mm tt") + " IST",
                HasImage = !string.IsNullOrEmpty(listing.ImageStorageKey),
                IsOwnListing = isOwner,
                IsAdmin = isAdmin,
                ModerationNote = (isOwner || isAdmin) ? listing.ModerationNote : null,
                IsContactRevealed = shouldRevealContact,
                RevealedSellerName = shouldRevealContact ? listing.SellerUser.FullName : null,
                RevealedSellerFlat = shouldRevealContact ? $"{listing.Building.Code}-{listing.Flat.FlatNumber}" : null,
                RevealedSellerPhone = shouldRevealContact
                    ? (!string.IsNullOrWhiteSpace(listing.SellerContactPhone) ? listing.SellerContactPhone : listing.SellerUser.PhoneNumber)
                    : null,
                RevealedSellerEmail = shouldRevealContact ? listing.SellerUser.Email : null
            };

            if (isAdmin)
            {
                var reports = await _context.MarketplaceListingReports
                    .AsNoTracking()
                    .Where(r => r.ListingId == id)
                    .Include(r => r.ReporterUser)
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .ToListAsync(ct);

                model.Reports = reports.Select(r => new MarketplaceReportItemViewModel
                {
                    Id = r.Id,
                    ReporterName = r.ReporterUser.FullName,
                    Reason = r.Reason,
                    CreatedAtDisplay = _tz.ToLocalTime(r.CreatedAtUtc).ToString("dd MMM, hh:mm tt") + " IST"
                }).ToList();
            }

            return View(model);
        }

        // ==================================================================
        // POST: /Marketplace/RevealContact/{id} (Intentional Contact Access)
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

            var isEligible = await _context.MarketplaceListings
                .AsNoTracking()
                .AnyAsync(m => m.Id == id && (m.Status == MarketplaceListingStatus.Active || isAdmin), ct);

            if (!isEligible) return NotFound();

            TempData["RevealSellerContactForId"] = id;
            return RedirectToAction(nameof(Details), new { id });
        }

        // ==================================================================
        // GET & POST: /Marketplace/Create
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            return View(new MarketplaceListingFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MarketplaceListingFormViewModel model, CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null) return Forbid();

            ValidatePriceRule(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? savedImageKey = null;
            try
            {
                if (model.Image != null && model.Image.Length > 0)
                {
                    savedImageKey = await _photoStorage.SavePhotoAsync(model.Image, ct);
                }

                var nowUtc = _tz.UtcNow;
                var isAdmin = User.IsInRole("Administrator");

                var listing = new MarketplaceListing
                {
                    Title = model.Title.Trim(),
                    Description = model.Description.Trim(),
                    Category = model.Category,
                    ListingType = model.ListingType,
                    PriceInr = model.ListingType == MarketplaceListingType.FreeGiveaway ? null : model.PriceInr,
                    Condition = model.Condition,
                    // Resident submissions start as PendingApproval; Admin submissions start Active
                    Status = isAdmin ? MarketplaceListingStatus.Active : MarketplaceListingStatus.PendingApproval,
                    SellerUserId = resident.UserId,
                    FlatId = resident.FlatId,
                    BuildingId = resident.Flat.BuildingId,
                    SellerContactPhone = model.SellerContactPhone?.Trim(),
                    ImageStorageKey = savedImageKey,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc
                };

                _context.MarketplaceListings.Add(listing);
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = isAdmin
                    ? "Your listing has been published to the marketplace."
                    : "Your listing has been submitted and is Pending Admin Approval before appearing in the public feed.";

                return RedirectToAction(nameof(Details), new { id = listing.Id });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(savedImageKey))
                {
                    await _photoStorage.DeletePhotoAsync(savedImageKey, ct);
                }

                ModelState.AddModelError(string.Empty, ex.Message.StartsWith("Image") || ex.Message.StartsWith("Invalid")
                    ? ex.Message
                    : "Unable to create the listing. Please check your inputs and try again.");

                return View(model);
            }
        }

        // ==================================================================
        // GET & POST: /Marketplace/Edit/{id} (Strict Seller Ownership Check)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var listing = await _context.MarketplaceListings
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id && m.SellerUserId == currentUserId, ct);

            if (listing == null) return NotFound();

            if (listing.Status == MarketplaceListingStatus.Sold || listing.Status == MarketplaceListingStatus.Withdrawn)
            {
                TempData["WarningMessage"] = "Sold or withdrawn listings cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new MarketplaceListingFormViewModel
            {
                Id = listing.Id,
                Title = listing.Title,
                Description = listing.Description,
                Category = listing.Category,
                ListingType = listing.ListingType,
                PriceInr = listing.PriceInr,
                Condition = listing.Condition,
                SellerContactPhone = listing.SellerContactPhone,
                HasExistingImage = !string.IsNullOrEmpty(listing.ImageStorageKey)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MarketplaceListingFormViewModel model, CancellationToken ct = default)
        {
            if (id != model.Id) return BadRequest();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var listing = await _context.MarketplaceListings
                .FirstOrDefaultAsync(m => m.Id == id && m.SellerUserId == currentUserId, ct);

            if (listing == null) return NotFound();

            if (listing.Status == MarketplaceListingStatus.Sold || listing.Status == MarketplaceListingStatus.Withdrawn)
            {
                TempData["WarningMessage"] = "Sold or withdrawn listings cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ValidatePriceRule(model);
            if (!ModelState.IsValid)
            {
                model.HasExistingImage = !string.IsNullOrEmpty(listing.ImageStorageKey);
                return View(model);
            }

            string? newImageKey = null;
            string? oldImageKey = listing.ImageStorageKey;

            try
            {
                if (model.Image != null && model.Image.Length > 0)
                {
                    newImageKey = await _photoStorage.SavePhotoAsync(model.Image, ct);
                    listing.ImageStorageKey = newImageKey;
                }

                listing.Title = model.Title.Trim();
                listing.Description = model.Description.Trim();
                listing.Category = model.Category;
                listing.ListingType = model.ListingType;
                listing.PriceInr = model.ListingType == MarketplaceListingType.FreeGiveaway ? null : model.PriceInr;
                listing.Condition = model.Condition;
                listing.SellerContactPhone = model.SellerContactPhone?.Trim();
                listing.UpdatedAtUtc = _tz.UtcNow;

                // Editing a moderated listing returns it to PendingApproval for renewed admin verification
                if (!User.IsInRole("Administrator"))
                {
                    listing.Status = MarketplaceListingStatus.PendingApproval;
                }

                await _context.SaveChangesAsync(ct);

                if (newImageKey != null && !string.IsNullOrEmpty(oldImageKey))
                {
                    await _photoStorage.DeletePhotoAsync(oldImageKey, ct);
                }

                TempData["SuccessMessage"] = User.IsInRole("Administrator")
                    ? "Listing updated."
                    : "Listing updated and queued for admin re-approval.";

                return RedirectToAction(nameof(Details), new { id = listing.Id });
            }
            catch (Exception ex)
            {
                if (newImageKey != null)
                {
                    await _photoStorage.DeletePhotoAsync(newImageKey, ct);
                }

                ModelState.AddModelError(string.Empty, ex.Message.StartsWith("Image") || ex.Message.StartsWith("Invalid")
                    ? ex.Message
                    : "Unable to save changes right now.");
                model.HasExistingImage = !string.IsNullOrEmpty(oldImageKey);
                return View(model);
            }
        }

        // ==================================================================
        // GET: /Marketplace/MyListings (Seller's Personal Listings)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> MyListings(CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null && !User.IsInRole("Administrator"))
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var items = await _context.MarketplaceListings
                .AsNoTracking()
                .Include(m => m.Building)
                .Where(m => m.SellerUserId == currentUserId)
                .OrderByDescending(m => m.UpdatedAtUtc)
                .ToListAsync(ct);

            var cards = items.Select(m => new MarketplaceCardViewModel
            {
                Id = m.Id,
                Title = m.Title,
                DescriptionSnippet = m.Description.Length > 120 ? m.Description.Substring(0, 120) + "..." : m.Description,
                Category = m.Category,
                CategoryDisplay = FormatCategoryLabel(m.Category),
                ListingType = m.ListingType,
                PriceInr = m.PriceInr,
                PriceDisplay = FormatPriceInr(m.ListingType, m.PriceInr),
                Condition = m.Condition,
                ConditionDisplay = FormatConditionLabel(m.Condition),
                Status = m.Status,
                TowerDisplay = $"Tower {m.Building.Code}",
                PostedDateDisplay = _tz.ToLocalTime(m.CreatedAtUtc).ToString("dd MMM yyyy"),
                HasImage = !string.IsNullOrEmpty(m.ImageStorageKey),
                IsOwnListing = true,
                ModerationNote = m.ModerationNote
            }).ToList();

            return View(cards);
        }

        // ==================================================================
        // POST: /Marketplace/UpdateOwnStatus (Mark Sold or Withdraw)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOwnStatus(int id, string actionType, CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var listing = await _context.MarketplaceListings
                .FirstOrDefaultAsync(m => m.Id == id && m.SellerUserId == currentUserId, ct);

            if (listing == null) return NotFound();

            var nowUtc = _tz.UtcNow;
            if (string.Equals(actionType, "MarkSold", StringComparison.OrdinalIgnoreCase))
            {
                listing.Status = MarketplaceListingStatus.Sold;
                listing.SoldOrWithdrawnAtUtc = nowUtc;
                listing.UpdatedAtUtc = nowUtc;
                TempData["SuccessMessage"] = $"'{listing.Title}' marked as Sold and removed from the active feed.";
            }
            else if (string.Equals(actionType, "Withdraw", StringComparison.OrdinalIgnoreCase))
            {
                var oldImage = listing.ImageStorageKey;
                listing.Status = MarketplaceListingStatus.Withdrawn;
                listing.SoldOrWithdrawnAtUtc = nowUtc;
                listing.ImageStorageKey = null;
                listing.UpdatedAtUtc = nowUtc;

                if (!string.IsNullOrEmpty(oldImage))
                {
                    await _photoStorage.DeletePhotoAsync(oldImage, ct);
                }

                TempData["SuccessMessage"] = $"'{listing.Title}' has been withdrawn from the marketplace.";
            }
            else
            {
                return BadRequest();
            }

            await _context.SaveChangesAsync(ct);
            return RedirectToAction(nameof(MyListings));
        }

        // ==================================================================
        // POST: /Marketplace/Report (Report Inappropriate Listing)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report(int id, string reason, CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            {
                TempData["ErrorMessage"] = "Please provide a brief reason (at least 5 characters) when reporting a listing.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var listing = await _context.MarketplaceListings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
            if (listing == null) return NotFound();

            var alreadyReported = await _context.MarketplaceListingReports
                .AnyAsync(r => r.ListingId == id && r.ReporterUserId == currentUserId, ct);

            if (alreadyReported)
            {
                TempData["WarningMessage"] = "You have already reported this listing for administrator review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                _context.MarketplaceListingReports.Add(new MarketplaceListingReport
                {
                    ListingId = id,
                    ReporterUserId = currentUserId,
                    Reason = reason.Trim(),
                    CreatedAtUtc = _tz.UtcNow
                });
                await _context.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = "Thank you. Your report has been sent to the Society Administrator for moderation.";
            }
            catch (DbUpdateException)
            {
                TempData["WarningMessage"] = "Your report was already recorded.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ==================================================================
        // GET & POST: /Marketplace/Admin (Administrator Moderation Console)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Admin(
            MarketplaceListingStatus? status,
            bool flaggedOnly = false,
            CancellationToken ct = default)
        {
            var baseQuery = _context.MarketplaceListings
                .AsNoTracking()
                .Include(m => m.Building)
                .Include(m => m.Flat)
                .Include(m => m.SellerUser);

            var pendingCount = await baseQuery.CountAsync(m => m.Status == MarketplaceListingStatus.PendingApproval, ct);
            var activeCount = await baseQuery.CountAsync(m => m.Status == MarketplaceListingStatus.Active, ct);
            var flaggedCount = await baseQuery.CountAsync(m => m.Reports.Any(), ct);

            var query = baseQuery.AsQueryable();
            if (status.HasValue)
            {
                query = query.Where(m => m.Status == status.Value);
            }

            if (flaggedOnly)
            {
                query = query.Where(m => m.Reports.Any());
            }

            var raw = await query
                .OrderBy(m => m.Status == MarketplaceListingStatus.PendingApproval ? 0 : 1)
                .ThenByDescending(m => m.Reports.Count)
                .ThenByDescending(m => m.UpdatedAtUtc)
                .Take(100)
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.Description,
                    m.Category,
                    m.ListingType,
                    m.PriceInr,
                    m.Condition,
                    m.Status,
                    m.ModerationNote,
                    BuildingCode = m.Building.Code,
                    FlatNumber = m.Flat.FlatNumber,
                    SellerName = m.SellerUser.FullName,
                    m.CreatedAtUtc,
                    HasImage = m.ImageStorageKey != null && m.ImageStorageKey != "",
                    ReportCount = m.Reports.Count()
                })
                .ToListAsync(ct);

            var cards = raw.Select(m => new MarketplaceCardViewModel
            {
                Id = m.Id,
                Title = m.Title,
                DescriptionSnippet = m.Description.Length > 120 ? m.Description.Substring(0, 120) + "..." : m.Description,
                Category = m.Category,
                CategoryDisplay = FormatCategoryLabel(m.Category),
                ListingType = m.ListingType,
                PriceInr = m.PriceInr,
                PriceDisplay = FormatPriceInr(m.ListingType, m.PriceInr),
                Condition = m.Condition,
                ConditionDisplay = FormatConditionLabel(m.Condition),
                Status = m.Status,
                TowerDisplay = $"Flat {m.BuildingCode}-{m.FlatNumber}",
                PostedDateDisplay = _tz.ToLocalTime(m.CreatedAtUtc).ToString("dd MMM HH:mm"),
                HasImage = m.HasImage,
                SellerInternalDisplay = $"{m.SellerName} ({m.BuildingCode}-{m.FlatNumber})",
                ReportCount = m.ReportCount,
                ModerationNote = m.ModerationNote
            }).ToList();

            var model = new AdminMarketplaceIndexViewModel
            {
                SelectedStatus = status,
                FlaggedOnly = flaggedOnly,
                PendingCount = pendingCount,
                ActiveCount = activeCount,
                FlaggedCount = flaggedCount,
                Listings = cards
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Moderate(
            int id,
            MarketplaceListingStatus targetStatus,
            string? note,
            CancellationToken ct = default)
        {
            var listing = await _context.MarketplaceListings.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (listing == null) return NotFound();

            if (targetStatus != MarketplaceListingStatus.Active &&
                targetStatus != MarketplaceListingStatus.Rejected &&
                targetStatus != MarketplaceListingStatus.Withdrawn)
            {
                return BadRequest("Unsupported moderation status.");
            }

            var nowUtc = _tz.UtcNow;
            listing.Status = targetStatus;
            listing.ModeratedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            listing.ModeratedAtUtc = nowUtc;
            listing.ModerationNote = note?.Trim();
            listing.UpdatedAtUtc = nowUtc;

            if (targetStatus == MarketplaceListingStatus.Withdrawn)
            {
                listing.SoldOrWithdrawnAtUtc = nowUtc;
            }

            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = $"Listing '{listing.Title}' updated to {targetStatus}.";
            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // GET: /Marketplace/Photo/{id} (Protected Item Photo Endpoint)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Photo(int id, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            if (!isAdmin)
            {
                var resident = await GetVerifiedResidentProfileAsync(ct);
                if (resident == null) return Forbid();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var listing = await _context.MarketplaceListings
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            if (listing == null || string.IsNullOrEmpty(listing.ImageStorageKey))
            {
                return NotFound();
            }

            // Only serve photos of Active listings, unless viewed by the seller or an administrator
            if (!isAdmin && listing.SellerUserId != currentUserId && listing.Status != MarketplaceListingStatus.Active)
            {
                return NotFound();
            }

            var result = await _photoStorage.GetPhotoAsync(listing.ImageStorageKey, ct);
            if (result == null) return NotFound();

            return File(result.Value.FileBytes, result.Value.ContentType);
        }

        private void ValidatePriceRule(MarketplaceListingFormViewModel model)
        {
            if (model.ListingType == MarketplaceListingType.ForSale)
            {
                if (!model.PriceInr.HasValue || model.PriceInr.Value <= 0)
                {
                    ModelState.AddModelError(nameof(model.PriceInr), "Please enter a valid price greater than ₹0 for items listed For Sale.");
                }
            }
            else
            {
                model.PriceInr = null;
            }
        }
    }
}