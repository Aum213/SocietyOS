using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SocietyOS.Data;
using SocietyOS.Models;
using SocietyOS.Services;
using SocietyOS.ViewModels;

namespace SocietyOS.Controllers
{
    [Authorize(Roles = "Resident,Administrator")]
    public class IssuesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IIssuePhotoStorageService _photoStorage;
        private const int PageSize = 10;

        public IssuesController(ApplicationDbContext context, IIssuePhotoStorageService photoStorage)
        {
            _context = context;
            _photoStorage = photoStorage;
        }

        private async Task<ResidentProfile?> GetVerifiedResidentProfileAsync(CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return null;

            return await _context.ResidentProfiles
                .Include(r => r.Flat)
                    .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(r => r.UserId == currentUserId && r.IsVerified, ct);
        }

        private static string FormatPrivacySafeLocation(
            IssueLocationScope scope,
            string? buildingCode,
            string? buildingName,
            string? locationDetails)
        {
            var baseLocation = scope switch
            {
                IssueLocationScope.FlatSpecific => !string.IsNullOrEmpty(buildingCode)
                    ? $"Private Unit • Tower {buildingCode}"
                    : "Private Residential Unit",
                IssueLocationScope.BuildingCommonArea => !string.IsNullOrEmpty(buildingName)
                    ? buildingName
                    : "Building Common Area",
                _ => "Community-Wide"
            };

            if (scope != IssueLocationScope.FlatSpecific && !string.IsNullOrWhiteSpace(locationDetails))
            {
                return $"{baseLocation} ({locationDetails})";
            }

            return baseLocation;
        }

        // ==================================================================
        // GET: /Issues (Resident Community Issue Feed)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            IssueStatus? status,
            IssueCategory? category,
            string sort = "most-voted",
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

            var query = _context.CommunityIssues
                .AsNoTracking()
                .Where(i => !i.IsDeleted)
                .Include(i => i.Building)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            if (category.HasValue)
            {
                query = query.Where(i => i.Category == category.Value);
            }

            var totalIssues = await query.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalIssues / (double)PageSize));
            page = Math.Min(page, totalPages);

            if (sort == "recent")
            {
                query = query.OrderByDescending(i => i.CreatedAtUtc);
            }
            else
            {
                sort = "most-voted";
                query = query
                    .OrderByDescending(i => i.Votes.Sum(v => (int?)v.VoteValue) ?? 0)
                    .ThenByDescending(i => i.CreatedAtUtc);
            }

            var rawIssues = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(i => new
                {
                    i.Id,
                    i.Title,
                    i.Description,
                    i.Category,
                    i.Status,
                    i.LocationScope,
                    BuildingCode = i.Building != null ? i.Building.Code : null,
                    BuildingName = i.Building != null ? i.Building.Name : null,
                    i.LocationDetails,
                    i.CreatedAtUtc,
                    HasPhoto = i.PhotoStorageKey != null && i.PhotoStorageKey != "",
                    Upvotes = i.Votes.Count(v => v.VoteValue == 1),
                    Downvotes = i.Votes.Count(v => v.VoteValue == -1),
                    CurrentUserVote = i.Votes
                        .Where(v => v.VoterUserId == currentUserId)
                        .Select(v => v.VoteValue)
                        .FirstOrDefault(),
                    CommentCount = i.Comments.Count()
                })
                .ToListAsync(ct);

            var cards = rawIssues.Select(i => new IssueCardViewModel
            {
                Id = i.Id,
                Title = i.Title,
                DescriptionSnippet = i.Description.Length > 140
                    ? i.Description.Substring(0, 140) + "..."
                    : i.Description,
                Category = i.Category,
                Status = i.Status,
                PrivacySafeLocation = FormatPrivacySafeLocation(i.LocationScope, i.BuildingCode, i.BuildingName, i.LocationDetails),
                CreatedAtUtc = i.CreatedAtUtc,
                HasPhoto = i.HasPhoto,
                Upvotes = i.Upvotes,
                Downvotes = i.Downvotes,
                NetVotes = i.Upvotes - i.Downvotes,
                CurrentUserVote = i.CurrentUserVote,
                CommentCount = i.CommentCount
            }).ToList();

            var viewModel = new IssueFeedViewModel
            {
                SelectedStatus = status,
                SelectedCategory = category,
                SortBy = sort,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalIssues = totalIssues,
                IsAdminView = false,
                Issues = cards
            };

            return View(viewModel);
        }

        // ==================================================================
        // GET & POST: /Issues/Create
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var model = new CreateIssueViewModel
            {
                BuildingId = resident.Flat.BuildingId,
                ResidentFlatDisplay = $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}"
            };

            await PopulateBuildingsAsync(model, ct);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateIssueViewModel model, CancellationToken ct = default)
        {
            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.ResidentFlatDisplay = $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}";
                await PopulateBuildingsAsync(model, ct);
                return View(model);
            }

            int? targetBuildingId = null;
            int? targetFlatId = null;

            if (model.LocationScope == IssueLocationScope.FlatSpecific)
            {
                targetBuildingId = resident.Flat.BuildingId;
                targetFlatId = resident.FlatId;
            }
            else if (model.LocationScope == IssueLocationScope.BuildingCommonArea)
            {
                var buildingExists = model.BuildingId.HasValue &&
                    await _context.Buildings.AnyAsync(b => b.Id == model.BuildingId.Value, ct);

                if (!buildingExists)
                {
                    ModelState.AddModelError(nameof(model.BuildingId), "Please select a valid building/tower.");
                    model.ResidentFlatDisplay = $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}";
                    await PopulateBuildingsAsync(model, ct);
                    return View(model);
                }
                targetBuildingId = model.BuildingId!.Value;
            }

            string? savedPhotoKey = null;

            try
            {
                if (model.Photo != null && model.Photo.Length > 0)
                {
                    savedPhotoKey = await _photoStorage.SavePhotoAsync(model.Photo, ct);
                }

                var nowUtc = DateTime.UtcNow;
                var issue = new CommunityIssue
                {
                    Title = model.Title.Trim(),
                    Description = model.Description.Trim(),
                    Category = model.Category,
                    Status = IssueStatus.Reported,
                    LocationScope = model.LocationScope,
                    LocationDetails = model.LocationDetails?.Trim(),
                    ReporterUserId = resident.UserId,
                    BuildingId = targetBuildingId,
                    FlatId = targetFlatId,
                    PhotoStorageKey = savedPhotoKey,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc
                };

                _context.CommunityIssues.Add(issue);
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Your issue has been reported to the community feed.";
                return RedirectToAction(nameof(Details), new { id = issue.Id });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(savedPhotoKey))
                {
                    await _photoStorage.DeletePhotoAsync(savedPhotoKey, ct);
                }

                ModelState.AddModelError(string.Empty, ex.Message.StartsWith("Photo") || ex.Message.StartsWith("Invalid")
                    ? ex.Message
                    : "Unable to save the issue right now. Please check your inputs and try again.");

                model.ResidentFlatDisplay = $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}";
                await PopulateBuildingsAsync(model, ct);
                return View(model);
            }
        }

        // ==================================================================
        // GET: /Issues/Details/{id}
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

            var issue = await _context.CommunityIssues
                .AsNoTracking()
                .Where(i => !i.IsDeleted)
                .Include(i => i.Building)
                .Include(i => i.Flat)
                    .ThenInclude(f => f != null ? f.Building : null)
                .Include(i => i.ReporterUser)
                .Include(i => i.Comments)
                .FirstOrDefaultAsync(i => i.Id == id, ct);

            if (issue == null) return NotFound();

            var upvotes = await _context.IssueVotes.CountAsync(v => v.IssueId == id && v.VoteValue == 1, ct);
            var downvotes = await _context.IssueVotes.CountAsync(v => v.IssueId == id && v.VoteValue == -1, ct);
            var userVote = await _context.IssueVotes
                .Where(v => v.IssueId == id && v.VoterUserId == currentUserId)
                .Select(v => v.VoteValue)
                .FirstOrDefaultAsync(ct);

            var adminRoleId = await _context.Roles
                .Where(r => r.Name == "Administrator")
                .Select(r => r.Id)
                .FirstOrDefaultAsync(ct);

            var adminUserIds = adminRoleId != null
                ? await _context.UserRoles
                    .Where(ur => ur.RoleId == adminRoleId)
                    .Select(ur => ur.UserId)
                    .ToHashSetAsync(ct)
                : new HashSet<string>();

            var commentItems = issue.Comments
                .OrderBy(c => c.CreatedAtUtc)
                .Select(c =>
                {
                    var isCommentByAdmin = adminUserIds.Contains(c.AuthorUserId);
                    var isOwn = c.AuthorUserId == currentUserId;
                    return new IssueCommentItemViewModel
                    {
                        Id = c.Id,
                        AuthorLabel = isCommentByAdmin
                            ? "Society Admin"
                            : (isOwn ? "You (Resident)" : "Verified Resident"),
                        IsAdminComment = isCommentByAdmin,
                        IsOwnComment = isOwn,
                        CommentText = c.CommentText,
                        CreatedAtUtc = c.CreatedAtUtc
                    };
                })
                .ToList();

            var isOwner = issue.ReporterUserId == currentUserId;

            var viewModel = new IssueDetailsViewModel
            {
                Id = issue.Id,
                Title = issue.Title,
                Description = issue.Description,
                Category = issue.Category,
                Status = issue.Status,
                PrivacySafeLocation = FormatPrivacySafeLocation(
                    issue.LocationScope,
                    issue.Building?.Code,
                    issue.Building?.Name,
                    issue.LocationDetails),
                CreatedAtUtc = issue.CreatedAtUtc,
                UpdatedAtUtc = issue.UpdatedAtUtc,
                ResolvedAtUtc = issue.ResolvedAtUtc,
                ResolutionSummary = issue.ResolutionSummary,
                HasPhoto = !string.IsNullOrEmpty(issue.PhotoStorageKey),
                Upvotes = upvotes,
                Downvotes = downvotes,
                NetVotes = upvotes - downvotes,
                CurrentUserVote = userVote,
                Comments = commentItems,
                IsAdmin = isAdmin,
                CanDelete = isOwner || isAdmin
            };

            if (isAdmin)
            {
                var reporterProfile = await _context.ResidentProfiles
                    .Include(r => r.Flat)
                        .ThenInclude(f => f.Building)
                    .FirstOrDefaultAsync(r => r.UserId == issue.ReporterUserId, ct);

                viewModel.InternalReporterName = issue.ReporterUser?.FullName ?? "Resident";
                viewModel.InternalReporterEmail = issue.ReporterUser?.Email;
                viewModel.InternalReporterFlat = reporterProfile != null
                    ? $"{reporterProfile.Flat.Building.Code}-{reporterProfile.Flat.FlatNumber}"
                    : (issue.Flat != null ? $"{issue.Flat.Building?.Code}-{issue.Flat.FlatNumber}" : "Unassigned");

                viewModel.StatusHistory = await _context.IssueStatusHistories
                    .AsNoTracking()
                    .Where(h => h.IssueId == id)
                    .Include(h => h.ChangedByAdmin)
                    .OrderByDescending(h => h.ChangedAtUtc)
                    .Select(h => new IssueStatusHistoryItemViewModel
                    {
                        PreviousStatus = h.PreviousStatus,
                        NewStatus = h.NewStatus,
                        AdminName = h.ChangedByAdmin.FullName,
                        InternalManagementNote = h.InternalManagementNote,
                        ChangedAtUtc = h.ChangedAtUtc
                    })
                    .ToListAsync(ct);
            }

            return View(viewModel);
        }

        // ==================================================================
        // POST: /Issues/Delete (Owner Resident or Administrator Only)
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Forbid();

            var isAdmin = User.IsInRole("Administrator");
            if (!isAdmin)
            {
                var resident = await GetVerifiedResidentProfileAsync(ct);
                if (resident == null) return Forbid();
            }

            var issue = await _context.CommunityIssues
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);

            if (issue == null) return NotFound();

            // Strict server-side ownership & role check
            if (!isAdmin && issue.ReporterUserId != currentUserId)
            {
                return NotFound();
            }

            var oldPhotoKey = issue.PhotoStorageKey;

            issue.IsDeleted = true;
            issue.DeletedAtUtc = DateTime.UtcNow;
            issue.DeletedByUserId = currentUserId;
            issue.PhotoStorageKey = null;
            issue.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(oldPhotoKey))
            {
                await _photoStorage.DeletePhotoAsync(oldPhotoKey, ct);
            }

            TempData["SuccessMessage"] = $"Issue '{issue.Title}' has been deleted.";
            return RedirectToAction(isAdmin ? nameof(Admin) : nameof(Index));
        }

        // ==================================================================
        // POST: /Issues/Vote
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote([FromForm] VoteIssueInputModel input, CancellationToken ct = default)
        {
            if (input.VoteValue != 1 && input.VoteValue != -1)
            {
                return BadRequest("Invalid vote value.");
            }

            var resident = await GetVerifiedResidentProfileAsync(ct);
            if (resident == null && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var issueExists = await _context.CommunityIssues
                .AnyAsync(i => i.Id == input.IssueId && !i.IsDeleted, ct);
            if (!issueExists) return NotFound();

            var existingVote = await _context.IssueVotes
                .FirstOrDefaultAsync(v => v.IssueId == input.IssueId && v.VoterUserId == currentUserId, ct);

            try
            {
                if (existingVote == null)
                {
                    _context.IssueVotes.Add(new IssueVote
                    {
                        IssueId = input.IssueId,
                        VoterUserId = currentUserId,
                        VoteValue = input.VoteValue,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
                else if (existingVote.VoteValue == input.VoteValue)
                {
                    _context.IssueVotes.Remove(existingVote);
                }
                else
                {
                    existingVote.VoteValue = input.VoteValue;
                    existingVote.UpdatedAtUtc = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
            }

            if (!string.IsNullOrEmpty(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
            {
                return Redirect(input.ReturnUrl);
            }

            return RedirectToAction(nameof(Details), new { id = input.IssueId });
        }

        // ==================================================================
        // POST: /Issues/AddComment
        // ==================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromForm] AddIssueCommentInputModel input, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            if (!isAdmin)
            {
                var resident = await GetVerifiedResidentProfileAsync(ct);
                if (resident == null) return Forbid();
            }

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.CommentText))
            {
                TempData["ErrorMessage"] = "Comment must be between 2 and 1000 characters.";
                return RedirectToAction(nameof(Details), new { id = input.IssueId });
            }

            var issueExists = await _context.CommunityIssues
                .AnyAsync(i => i.Id == input.IssueId && !i.IsDeleted, ct);
            if (!issueExists) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            _context.IssueComments.Add(new IssueComment
            {
                IssueId = input.IssueId,
                AuthorUserId = currentUserId,
                CommentText = input.CommentText.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = "Your comment was added.";
            return RedirectToAction(nameof(Details), new { id = input.IssueId });
        }

        // ==================================================================
        // GET: /Issues/Admin
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Admin(
            IssueStatus? status,
            IssueCategory? category,
            string? q,
            string sort = "most-voted",
            int page = 1,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            var query = _context.CommunityIssues
                .AsNoTracking()
                .Where(i => !i.IsDeleted)
                .Include(i => i.Building)
                .Include(i => i.Flat)
                    .ThenInclude(f => f != null ? f.Building : null)
                .Include(i => i.ReporterUser)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            if (category.HasValue)
            {
                query = query.Where(i => i.Category == category.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim();
                query = query.Where(i => i.Title.Contains(search) || i.Description.Contains(search));
            }

            var totalIssues = await query.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalIssues / (double)PageSize));
            page = Math.Min(page, totalPages);

            if (sort == "recent")
            {
                query = query.OrderByDescending(i => i.CreatedAtUtc);
            }
            else
            {
                sort = "most-voted";
                query = query
                    .OrderByDescending(i => i.Votes.Sum(v => (int?)v.VoteValue) ?? 0)
                    .ThenByDescending(i => i.CreatedAtUtc);
            }

            var rawIssues = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(i => new
                {
                    i.Id,
                    i.Title,
                    i.Description,
                    i.Category,
                    i.Status,
                    i.LocationScope,
                    BuildingCode = i.Building != null ? i.Building.Code : null,
                    BuildingName = i.Building != null ? i.Building.Name : null,
                    FlatDisplay = i.Flat != null && i.Flat.Building != null
                        ? i.Flat.Building.Code + "-" + i.Flat.FlatNumber
                        : null,
                    ReporterName = i.ReporterUser.FullName,
                    i.LocationDetails,
                    i.CreatedAtUtc,
                    HasPhoto = i.PhotoStorageKey != null && i.PhotoStorageKey != "",
                    Upvotes = i.Votes.Count(v => v.VoteValue == 1),
                    Downvotes = i.Votes.Count(v => v.VoteValue == -1),
                    CommentCount = i.Comments.Count()
                })
                .ToListAsync(ct);

            var cards = rawIssues.Select(i => new IssueCardViewModel
            {
                Id = i.Id,
                Title = i.Title,
                DescriptionSnippet = i.Description.Length > 140
                    ? i.Description.Substring(0, 140) + "..."
                    : i.Description,
                Category = i.Category,
                Status = i.Status,
                PrivacySafeLocation = FormatPrivacySafeLocation(i.LocationScope, i.BuildingCode, i.BuildingName, i.LocationDetails),
                CreatedAtUtc = i.CreatedAtUtc,
                HasPhoto = i.HasPhoto,
                Upvotes = i.Upvotes,
                Downvotes = i.Downvotes,
                NetVotes = i.Upvotes - i.Downvotes,
                CommentCount = i.CommentCount,
                AdminReporterName = i.ReporterName,
                AdminReporterFlat = i.FlatDisplay ?? "Verified Resident"
            }).ToList();

            var viewModel = new IssueFeedViewModel
            {
                SelectedStatus = status,
                SelectedCategory = category,
                SortBy = sort,
                SearchQuery = q,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalIssues = totalIssues,
                IsAdminView = true,
                Issues = cards
            };

            return View("Admin", viewModel);
        }

        // ==================================================================
        // POST: /Issues/UpdateStatus
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromForm] AdminStatusUpdateInputModel input, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please check the status update fields.";
                return RedirectToAction(nameof(Details), new { id = input.IssueId });
            }

            var issue = await _context.CommunityIssues
                .FirstOrDefaultAsync(i => i.Id == input.IssueId && !i.IsDeleted, ct);
            if (issue == null) return NotFound();

            var previousStatus = issue.Status;
            var newStatus = input.NewStatus;

            if (previousStatus == newStatus && string.IsNullOrWhiteSpace(input.InternalManagementNote) && string.IsNullOrWhiteSpace(input.ResolutionSummary))
            {
                TempData["WarningMessage"] = "Issue is already in the selected status.";
                return RedirectToAction(nameof(Details), new { id = input.IssueId });
            }

            bool isValidTransition = (previousStatus, newStatus) switch
            {
                (IssueStatus.Reported, IssueStatus.Verified) => true,
                (IssueStatus.Reported, IssueStatus.InProgress) => true,
                (IssueStatus.Reported, IssueStatus.Resolved) => true,
                (IssueStatus.Verified, IssueStatus.InProgress) => true,
                (IssueStatus.Verified, IssueStatus.Resolved) => true,
                (IssueStatus.InProgress, IssueStatus.Resolved) => true,
                (IssueStatus.Resolved, IssueStatus.InProgress) => true,
                _ when previousStatus == newStatus => true,
                _ => false
            };

            if (!isValidTransition)
            {
                TempData["ErrorMessage"] = $"Invalid workflow transition from {previousStatus} to {newStatus}.";
                return RedirectToAction(nameof(Details), new { id = input.IssueId });
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = DateTime.UtcNow;

            using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                issue.Status = newStatus;
                issue.UpdatedAtUtc = nowUtc;

                if (!string.IsNullOrWhiteSpace(input.ResolutionSummary))
                {
                    issue.ResolutionSummary = input.ResolutionSummary.Trim();
                }

                if (newStatus == IssueStatus.Resolved)
                {
                    issue.ResolvedAtUtc = nowUtc;
                }
                else if (previousStatus == IssueStatus.Resolved && newStatus == IssueStatus.InProgress)
                {
                    issue.ResolvedAtUtc = null;
                }

                _context.IssueStatusHistories.Add(new IssueStatusHistory
                {
                    IssueId = issue.Id,
                    PreviousStatus = previousStatus,
                    NewStatus = newStatus,
                    ChangedByAdminId = adminId,
                    InternalManagementNote = input.InternalManagementNote?.Trim(),
                    ChangedAtUtc = nowUtc
                });

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                TempData["SuccessMessage"] = $"Issue status updated to {newStatus}.";
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(ct);
                TempData["ErrorMessage"] = "Another administrator updated this issue at the same time. Please review the latest status.";
            }

            return RedirectToAction(nameof(Details), new { id = input.IssueId });
        }

        // ==================================================================
        // GET: /Issues/Photo/{id}
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

            var issue = await _context.CommunityIssues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);

            if (issue == null || string.IsNullOrEmpty(issue.PhotoStorageKey))
            {
                return NotFound();
            }

            var photoResult = await _photoStorage.GetPhotoAsync(issue.PhotoStorageKey, ct);
            if (photoResult == null) return NotFound();

            return File(photoResult.Value.FileBytes, photoResult.Value.ContentType);
        }

        private async Task PopulateBuildingsAsync(CreateIssueViewModel model, CancellationToken ct)
        {
            var buildings = await _context.Buildings
                .OrderBy(b => b.Code)
                .ToListAsync(ct);

            model.Buildings = buildings.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = $"{b.Code} — {b.Name}"
            }).ToList();
        }
    }
}