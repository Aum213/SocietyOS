using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
    [Authorize]
    public class VisitorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IVisitorPhotoStorageService _photoStorage;

        public VisitorsController(ApplicationDbContext context, IVisitorPhotoStorageService photoStorage)
        {
            _context = context;
            _photoStorage = photoStorage;
        }

        // ==================================================================
        // Helper: Resolve authenticated user's verified flat association
        // ==================================================================
        private async Task<ResidentProfile?> GetVerifiedResidentProfileAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return null;

            return await _context.ResidentProfiles
                .Include(r => r.Flat)
                    .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(r => r.UserId == currentUserId && r.IsVerified);
        }

        // ==================================================================
        // GET: /Visitors (Role-Aware Gate Landing Entry Point)
        // ==================================================================
        [HttpGet]
        public IActionResult Index()
        {
            if (User.IsInRole("SecurityGuard"))
            {
                return RedirectToAction(nameof(Register));
            }

            return RedirectToAction(nameof(Pending));
        }


        // ==================================================================
        // GET: /Visitors/Pending
        // GET: /Visitors/Approved
        // GET: /Visitors/Rejected
        // ==================================================================
        [Authorize(Roles = "Resident,Administrator")]
        [HttpGet]
        public async Task<IActionResult> Pending() => await RenderResidentInboxAsync("Pending", VisitorStatus.Pending);

        [Authorize(Roles = "Resident,Administrator")]
        [HttpGet]
        public async Task<IActionResult> Approved() => await RenderResidentInboxAsync("Approved", VisitorStatus.Approved);

        [Authorize(Roles = "Resident,Administrator")]
        [HttpGet]
        public async Task<IActionResult> Rejected() => await RenderResidentInboxAsync("Rejected", VisitorStatus.Rejected);

        private async Task<IActionResult> RenderResidentInboxAsync(string activeTab, VisitorStatus filterStatus)
        {
            var resident = await GetVerifiedResidentProfileAsync();
            if (resident == null)
            {
                return View("UnverifiedResidentNotice");
            }

            var nowUtc = DateTime.UtcNow;

            // Base query scoped strictly to the resident's verified flat and unexpired records
            var baseQuery = _context.VisitorRequests
                .Where(v => v.FlatId == resident.FlatId && v.ExpiresAtUtc > nowUtc);

            var pendingCount = await baseQuery.CountAsync(v => v.Status == VisitorStatus.Pending);
            var approvedCount = await baseQuery.CountAsync(v => v.Status == VisitorStatus.Approved);
            var rejectedCount = await baseQuery.CountAsync(v => v.Status == VisitorStatus.Rejected);

            var requests = await baseQuery
                .Where(v => v.Status == filterStatus)
                .OrderByDescending(v => v.CreatedAtUtc)
                .Select(v => new VisitorCardViewModel
                {
                    Id = v.Id,
                    VisitorName = v.VisitorName,
                    Purpose = v.Purpose,
                    PurposeDetails = v.PurposeDetails,
                    AccompanyingPersons = v.AccompanyingPersons,
                    Status = v.Status,
                    CreatedAtUtc = v.CreatedAtUtc,
                    ExpiresAtUtc = v.ExpiresAtUtc,
                    DecisionAtUtc = v.DecisionAtUtc,
                    DecidedBy = v.DecisionByResident != null ? v.DecisionByResident.FullName : null
                })
                .ToListAsync();

            var model = new ResidentVisitorInboxViewModel
            {
                FlatDisplay = $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}",
                BuildingName = resident.Flat.Building.Name,
                ActiveTab = activeTab,
                PendingCount = pendingCount,
                ApprovedCount = approvedCount,
                RejectedCount = rejectedCount,
                Requests = requests
            };

            return View("Inbox", model);
        }

        // ==================================================================
        // GET: /Visitors/Details/{id}
        // ==================================================================
        [Authorize(Roles = "Resident,Administrator,SecurityGuard")]
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var resident = await GetVerifiedResidentProfileAsync();
            var isGuardOrAdmin = User.IsInRole("SecurityGuard") || User.IsInRole("Administrator");

            var nowUtc = DateTime.UtcNow;
            var req = await _context.VisitorRequests
                .Include(v => v.Flat)
                    .ThenInclude(f => f.Building)
                .Include(v => v.DecisionByResident)
                .Include(v => v.RegisteredByGuard)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (req == null) return NotFound();

            if (req.ExpiresAtUtc <= nowUtc)
            {
                return NotFound("Visitor request has expired in accordance with the 48-hour retention rule.");
            }

            if (!isGuardOrAdmin)
            {
                if (resident == null || req.FlatId != resident.FlatId)
                {
                    return NotFound();
                }
            }

            var viewModel = new VisitorCardViewModel
            {
                Id = req.Id,
                VisitorName = req.VisitorName,
                Purpose = req.Purpose,
                PurposeDetails = req.PurposeDetails,
                AccompanyingPersons = req.AccompanyingPersons,
                Status = req.Status,
                CreatedAtUtc = req.CreatedAtUtc,
                ExpiresAtUtc = req.ExpiresAtUtc,
                DecisionAtUtc = req.DecisionAtUtc,
                DecidedBy = req.DecisionByResident?.FullName,
                RegisteredByGuardName = req.RegisteredByGuard?.FullName
            };

            return View(viewModel);
        }

        // ==================================================================
        // POST: /Visitors/Decide (Atomic Approval / Rejection)
        // ==================================================================
        [Authorize(Roles = "Resident,Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decide([FromForm] VisitorDecisionInputModel input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid decision submission.";
                return RedirectToAction(nameof(Pending));
            }

            var resident = await GetVerifiedResidentProfileAsync();
            if (resident == null)
            {
                return Forbid();
            }

            var nowUtc = DateTime.UtcNow;

            var req = await _context.VisitorRequests
                .FirstOrDefaultAsync(v => v.Id == input.RequestId && v.FlatId == resident.FlatId);

            if (req == null || req.ExpiresAtUtc <= nowUtc)
            {
                TempData["ErrorMessage"] = "Visitor request was not found or has expired.";
                return RedirectToAction(nameof(Pending));
            }

            if (req.Status != VisitorStatus.Pending)
            {
                TempData["WarningMessage"] = $"This request has already been decided as {req.Status}.";
                return RedirectToAction(nameof(Pending));
            }

            var targetStatus = input.Decision.Equals("Approve", StringComparison.OrdinalIgnoreCase)
                ? VisitorStatus.Approved
                : VisitorStatus.Rejected;

            req.Status = targetStatus;
            req.DecisionAtUtc = nowUtc;
            req.DecisionByResidentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            req.DecisionNotes = input.DecisionNotes?.Trim();

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = targetStatus == VisitorStatus.Approved
                    ? $"Visitor {req.VisitorName} was approved for entry."
                    : $"Visitor {req.VisitorName} was denied entry.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "A conflicting decision was recorded by another household member.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the visitor request. Please try again.";
            }

            return RedirectToAction(nameof(Pending));
        }

        // ==================================================================
        // GET: /Visitors/Register (Guard Intake)
        // ==================================================================
        [Authorize(Roles = "SecurityGuard,Administrator")]
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var model = new RegisterVisitorViewModel();
            await PopulateBuildingAndFlatListsAsync(model);
            return View(model);
        }

        // ==================================================================
        // POST: /Visitors/Register
        // ==================================================================
        [Authorize(Roles = "SecurityGuard,Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVisitorViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateBuildingAndFlatListsAsync(model);
                return View(model);
            }

            var targetFlat = await _context.Flats
                .Include(f => f.Building)
                .FirstOrDefaultAsync(f => f.Id == model.FlatId && f.BuildingId == model.BuildingId);

            if (targetFlat == null)
            {
                ModelState.AddModelError("FlatId", "The selected flat is invalid for this building.");
                await PopulateBuildingAndFlatListsAsync(model);
                return View(model);
            }

            var guardUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? savedKey = null;

            try
            {
                savedKey = await _photoStorage.SavePhotoAsync(model.Photo);

                var nowUtc = DateTime.UtcNow;
                var visitorRequest = new VisitorRequest
                {
                    VisitorName = model.VisitorName.Trim(),
                    FlatId = targetFlat.Id,
                    Purpose = model.Purpose,
                    PurposeDetails = model.PurposeDetails?.Trim(),
                    AccompanyingPersons = model.AccompanyingPersons,
                    PhotoStorageKey = savedKey,
                    CreatedAtUtc = nowUtc,
                    ExpiresAtUtc = nowUtc.AddHours(48),
                    Status = VisitorStatus.Pending,
                    RegisteredByGuardId = guardUserId
                };

                _context.VisitorRequests.Add(visitorRequest);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Confirmation), new { id = visitorRequest.Id });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(savedKey))
                {
                    await _photoStorage.DeletePhotoAsync(savedKey);
                }

                ModelState.AddModelError(string.Empty, ex.Message.StartsWith("Photo") || ex.Message.StartsWith("Invalid")
                    ? ex.Message
                    : "An error occurred while saving the visitor request. Please try again.");

                await PopulateBuildingAndFlatListsAsync(model);
                return View(model);
            }
        }

        // ==================================================================
        // GET: /Visitors/Confirmation/{id} (Live Status for Guards)
        // ==================================================================
        [Authorize(Roles = "SecurityGuard,Administrator")]
        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            var req = await _context.VisitorRequests
                .Include(v => v.Flat)
                    .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (req == null) return NotFound();

            var viewModel = new VisitorConfirmationViewModel
            {
                RequestId = req.Id,
                VisitorName = req.VisitorName,
                FlatDisplay = $"{req.Flat.Building.Code}-{req.Flat.FlatNumber}",
                BuildingName = req.Flat.Building.Name,
                Purpose = req.Purpose,
                AccompanyingPersons = req.AccompanyingPersons,
                Status = req.Status.ToString(), // Fetches live decision status from database
                CreatedAtUtc = req.CreatedAtUtc,
                ExpiresAtUtc = req.ExpiresAtUtc
            };

            return View(viewModel);
        }

        // ==================================================================
        // GET: /Visitors/Photo/{id} (Protected Photo Streaming)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Photo(int id)
        {
            var req = await _context.VisitorRequests.FindAsync(id);
            if (req == null) return NotFound();

            if (req.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return NotFound("Visitor record has expired in accordance with the 48-hour retention policy.");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isGuardOrAdmin = User.IsInRole("SecurityGuard") || User.IsInRole("Administrator");

            if (!isGuardOrAdmin)
            {
                var isResidentOfFlat = await _context.ResidentProfiles
                    .AnyAsync(r => r.UserId == currentUserId && r.FlatId == req.FlatId && r.IsVerified);

                if (!isResidentOfFlat)
                {
                    return Forbid();
                }
            }

            var fileResult = await _photoStorage.GetPhotoAsync(req.PhotoStorageKey);
            if (fileResult == null) return NotFound("Visitor image file not found.");

            return File(fileResult.Value.FileBytes, fileResult.Value.ContentType);
        }

        // ==================================================================
        // AJAX: /Visitors/GetFlatsByBuilding?buildingId=1
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> GetFlatsByBuilding(int buildingId)
        {
            var flats = await _context.Flats
                .Where(f => f.BuildingId == buildingId)
                .OrderBy(f => f.Floor)
                .ThenBy(f => f.FlatNumber)
                .Select(f => new { id = f.Id, text = $"Flat {f.FlatNumber} (Floor {f.Floor})" })
                .ToListAsync();

            return Json(flats);
        }

        private async Task PopulateBuildingAndFlatListsAsync(RegisterVisitorViewModel model)
        {
            var buildings = await _context.Buildings
                .OrderBy(b => b.Code)
                .ToListAsync();

            model.Buildings = buildings.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = $"{b.Code} — {b.Name}"
            }).ToList();

            if (model.BuildingId == 0 && buildings.Any())
            {
                model.BuildingId = buildings.First().Id;
            }

            var flats = await _context.Flats
                .Where(f => f.BuildingId == model.BuildingId)
                .OrderBy(f => f.Floor)
                .ThenBy(f => f.FlatNumber)
                .ToListAsync();

            model.Flats = flats.Select(f => new SelectListItem
            {
                Value = f.Id.ToString(),
                Text = $"Flat {f.FlatNumber} (Floor {f.Floor})"
            }).ToList();
        }
    }
}