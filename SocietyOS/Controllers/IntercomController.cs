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
    [Authorize]
    public class IntercomController : Controller
    {
        private readonly ApplicationDbContext _context;

        public IntercomController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<bool> IsAuthorizedForDirectoryAsync(CancellationToken ct)
        {
            if (User.IsInRole("Administrator") || User.IsInRole("SecurityGuard"))
            {
                return true;
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return false;

            return await _context.ResidentProfiles
                .AsNoTracking()
                .AnyAsync(r => r.UserId == currentUserId && r.IsVerified, ct);
        }

        public static string FormatCategoryLabel(IntercomCategory category) => category switch
        {
            IntercomCategory.SecurityDesk => "Security Desk",
            IntercomCategory.ManagementOffice => "Society Management Office",
            IntercomCategory.Maintenance => "Maintenance",
            IntercomCategory.Housekeeping => "Housekeeping",
            _ => "Other Society Services"
        };

        // ==================================================================
        // GET: /Intercom (Resident & Staff Intercom Directory)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            IntercomCategory? category,
            string? q,
            CancellationToken ct = default)
        {
            if (!await IsAuthorizedForDirectoryAsync(ct))
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var query = _context.SocietyEmergencyContacts
                .AsNoTracking()
                .Where(c => c.IsActive);

            if (category.HasValue)
            {
                query = query.Where(c => c.Category == category.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(c =>
                    c.DisplayName.Contains(term) ||
                    (c.Description != null && c.Description.Contains(term)));
            }

            var raw = await query
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.DisplayName)
                .ToListAsync(ct);

            var model = new IntercomDirectoryViewModel
            {
                SelectedCategory = category,
                SearchQuery = q,
                Contacts = raw.Select(c => new IntercomContactItemViewModel
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    Category = c.Category,
                    CategoryDisplay = FormatCategoryLabel(c.Category),
                    PhoneNumber = c.PhoneNumber,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ShowOnEmergencySos = c.ShowOnEmergencySos,
                    DisplayOrder = c.DisplayOrder
                }).ToList()
            };

            return View(model);
        }

        // ==================================================================
        // GET & POST: /Intercom/Admin (Administrator Contact Management)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Admin(CancellationToken ct = default)
        {
            var raw = await _context.SocietyEmergencyContacts
                .AsNoTracking()
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.DisplayName)
                .ToListAsync(ct);

            var model = new AdminIntercomIndexViewModel
            {
                Contacts = raw.Select(c => new IntercomContactItemViewModel
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    Category = c.Category,
                    CategoryDisplay = FormatCategoryLabel(c.Category),
                    PhoneNumber = c.PhoneNumber,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ShowOnEmergencySos = c.ShowOnEmergencySos,
                    DisplayOrder = c.DisplayOrder
                }).ToList()
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind(Prefix = "NewContact")] IntercomContactFormViewModel input,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                var raw = await _context.SocietyEmergencyContacts
                    .AsNoTracking()
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync(ct);

                return View("Admin", new AdminIntercomIndexViewModel
                {
                    NewContact = input,
                    Contacts = raw.Select(c => new IntercomContactItemViewModel
                    {
                        Id = c.Id,
                        DisplayName = c.DisplayName,
                        Category = c.Category,
                        CategoryDisplay = FormatCategoryLabel(c.Category),
                        PhoneNumber = c.PhoneNumber,
                        Description = c.Description,
                        IsActive = c.IsActive,
                        ShowOnEmergencySos = c.ShowOnEmergencySos,
                        DisplayOrder = c.DisplayOrder
                    }).ToList()
                });
            }

            var contact = new SocietyEmergencyContact
            {
                DisplayName = input.DisplayName.Trim(),
                Category = input.Category,
                PhoneNumber = input.PhoneNumber.Trim(),
                Description = input.Description?.Trim(),
                DisplayOrder = input.DisplayOrder,
                IsActive = input.IsActive,
                ShowOnEmergencySos = input.ShowOnEmergencySos,
                UpdatedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                UpdatedAtUtc = DateTime.UtcNow
            };

            _context.SocietyEmergencyContacts.Add(contact);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = $"Directory contact '{contact.DisplayName}' added.";
            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // GET & POST: /Intercom/Edit/{id}
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
        {
            var c = await _context.SocietyEmergencyContacts.FindAsync(new object[] { id }, ct);
            if (c == null) return NotFound();

            var model = new IntercomContactFormViewModel
            {
                Id = c.Id,
                DisplayName = c.DisplayName,
                Category = c.Category,
                PhoneNumber = c.PhoneNumber,
                Description = c.Description,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                ShowOnEmergencySos = c.ShowOnEmergencySos
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IntercomContactFormViewModel model, CancellationToken ct = default)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);

            var c = await _context.SocietyEmergencyContacts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c == null) return NotFound();

            c.DisplayName = model.DisplayName.Trim();
            c.Category = model.Category;
            c.PhoneNumber = model.PhoneNumber.Trim();
            c.Description = model.Description?.Trim();
            c.DisplayOrder = model.DisplayOrder;
            c.IsActive = model.IsActive;
            c.ShowOnEmergencySos = model.ShowOnEmergencySos;
            c.UpdatedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            c.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = $"Updated directory contact '{c.DisplayName}'.";
            return RedirectToAction(nameof(Admin));
        }

        // ==================================================================
        // POST: /Intercom/ToggleActive/{id}
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, CancellationToken ct = default)
        {
            var c = await _context.SocietyEmergencyContacts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c != null)
            {
                c.IsActive = !c.IsActive;
                c.UpdatedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                c.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = $"Contact '{c.DisplayName}' is now {(c.IsActive ? "Active" : "Inactive")}.";
            }
            return RedirectToAction(nameof(Admin));
        }
    }
}