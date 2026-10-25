using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
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
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISocietyTimeZoneService _tz;

        public MaintenanceController(ApplicationDbContext context, ISocietyTimeZoneService tz)
        {
            _context = context;
            _tz = tz;
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

        public static string FormatInr(decimal amount) =>
            string.Format(new CultureInfo("en-IN"), "₹{0:N2}", amount);

        public static string FormatBillingPeriod(int year, int month) =>
            new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        public static string FormatPaymentMethod(MaintenancePaymentMethod method) => method switch
        {
            MaintenancePaymentMethod.BankTransferNeftImps => "Bank Transfer (NEFT / IMPS)",
            MaintenancePaymentMethod.UpiTransfer => "UPI Transfer",
            MaintenancePaymentMethod.Cheque => "Cheque",
            MaintenancePaymentMethod.Cash => "Cash at Society Office",
            _ => "Other Method"
        };

        private static MaintenanceBillCardViewModel MapBillToCard(
            MaintenanceBill bill,
            DateOnly todayLocal)
        {
            var addedCharges = bill.Adjustments
                .Where(a => a.Type == MaintenanceAdjustmentType.AdditionalCharge)
                .Sum(a => a.AmountInr);

            var credits = bill.Adjustments
                .Where(a => a.Type == MaintenanceAdjustmentType.CreditOrWaiver)
                .Sum(a => a.AmountInr);

            var netAdjustments = addedCharges - credits;
            var netBill = Math.Max(0m, decimal.Round(bill.OriginalAmountInr + netAdjustments, 2));

            var totalPaid = decimal.Round(
                bill.Payments.Where(p => !p.IsReversed).Sum(p => p.AmountReceivedInr), 2);

            var outstanding = bill.IsVoid
                ? 0m
                : Math.Max(0m, decimal.Round(netBill - totalPaid, 2));

            MaintenanceBillStatus status;
            if (bill.IsVoid)
            {
                status = MaintenanceBillStatus.Void;
            }
            else if (outstanding == 0m)
            {
                status = MaintenanceBillStatus.Paid;
            }
            else if (totalPaid > 0m)
            {
                status = MaintenanceBillStatus.PartiallyPaid;
            }
            else
            {
                status = MaintenanceBillStatus.Unpaid;
            }

            var isOverdue = !bill.IsVoid && outstanding > 0m && bill.DueDateLocal < todayLocal;

            return new MaintenanceBillCardViewModel
            {
                Id = bill.Id,
                BillNumber = bill.BillNumber,
                FlatDisplay = $"{bill.Flat.Building.Code}-{bill.Flat.FlatNumber}",
                BuildingName = bill.Flat.Building.Name,
                BillingYear = bill.BillingYear,
                BillingMonth = bill.BillingMonth,
                BillingPeriodDisplay = FormatBillingPeriod(bill.BillingYear, bill.BillingMonth),
                IssueDateLocal = bill.IssueDateLocal,
                DueDateLocal = bill.DueDateLocal,
                Description = bill.Description,
                OriginalAmountInr = bill.OriginalAmountInr,
                NetAdjustmentsInr = netAdjustments,
                NetBillAmountInr = netBill,
                TotalPaidInr = totalPaid,
                OutstandingBalanceInr = outstanding,
                Status = status,
                IsOverdue = isOverdue,
                IsVoid = bill.IsVoid
            };
        }

        // ==================================================================
        // GET: /Maintenance (Resident Maintenance Dues & Bills Dashboard)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            int? year,
            string? statusFilter,
            CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            var resident = await GetVerifiedResidentProfileAsync(ct);

            if (resident == null)
            {
                if (isAdmin)
                {
                    return RedirectToAction(nameof(Admin));
                }
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);

            // Load bills strictly for the resident's verified flat
            var flatBills = await _context.MaintenanceBills
                .AsNoTracking()
                .Include(b => b.Flat)
                    .ThenInclude(f => f.Building)
                .Include(b => b.Payments)
                .Include(b => b.Adjustments)
                .Where(b => b.FlatId == resident.FlatId && !b.IsVoid)
                .OrderByDescending(b => b.BillingYear)
                .ThenByDescending(b => b.BillingMonth)
                .ToListAsync(ct);

            var allCards = flatBills.Select(b => MapBillToCard(b, todayLocal)).ToList();

            var filtered = allCards.AsEnumerable();
            if (year.HasValue)
            {
                filtered = filtered.Where(c => c.BillingYear == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                filtered = statusFilter.ToLowerInvariant() switch
                {
                    "open" => filtered.Where(c => c.OutstandingBalanceInr > 0m),
                    "overdue" => filtered.Where(c => c.IsOverdue),
                    "paid" => filtered.Where(c => c.Status == MaintenanceBillStatus.Paid),
                    _ => filtered
                };
            }

            var model = new ResidentMaintenanceDashboardViewModel
            {
                FlatDisplay = $"{resident.Flat.Building.Code}-{resident.Flat.FlatNumber}",
                BuildingName = resident.Flat.Building.Name,
                TotalOutstandingInr = allCards.Sum(c => c.OutstandingBalanceInr),
                UnpaidOrPartialCount = allCards.Count(c => c.OutstandingBalanceInr > 0m),
                OverdueCount = allCards.Count(c => c.IsOverdue),
                PaidCount = allCards.Count(c => c.Status == MaintenanceBillStatus.Paid),
                SelectedYear = year,
                SelectedStatusFilter = statusFilter,
                Bills = filtered.ToList()
            };

            return View(model);
        }

        // ==================================================================
        // GET: /Maintenance/Details/{id} (Bill Ledger, Printable Invoice & History)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            var resident = await GetVerifiedResidentProfileAsync(ct);

            if (!isAdmin && resident == null)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var bill = await _context.MaintenanceBills
                .AsNoTracking()
                .Include(b => b.Flat)
                    .ThenInclude(f => f.Building)
                .Include(b => b.Payments)
                    .ThenInclude(p => p.RecordedByAdmin)
                .Include(b => b.Adjustments)
                    .ThenInclude(a => a.CreatedByAdmin)
                .FirstOrDefaultAsync(b => b.Id == id, ct);

            if (bill == null) return NotFound();

            // Strict flat ownership isolation: non-admin residents can only view bills for their own verified flat
            if (!isAdmin && bill.FlatId != resident!.FlatId)
            {
                return NotFound();
            }

            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);
            var card = MapBillToCard(bill, todayLocal);

            var model = new MaintenanceBillDetailsViewModel
            {
                Bill = card,
                VoidReason = bill.VoidReason,
                IsAdmin = isAdmin,
                Adjustments = bill.Adjustments
                    .OrderBy(a => a.CreatedAtUtc)
                    .Select(a => new MaintenanceAdjustmentItemViewModel
                    {
                        Id = a.Id,
                        Type = a.Type,
                        AmountInr = a.AmountInr,
                        Reason = a.Reason,
                        AdminName = a.CreatedByAdmin.FullName,
                        CreatedAtLocalDisplay = _tz.ToLocalTime(a.CreatedAtUtc).ToString("dd MMM yyyy, hh:mm tt") + " IST"
                    }).ToList(),
                Payments = bill.Payments
                    .OrderByDescending(p => p.RecordedAtUtc)
                    .Select(p => new MaintenancePaymentItemViewModel
                    {
                        Id = p.Id,
                        PaymentReference = p.PaymentReference,
                        AmountReceivedInr = p.AmountReceivedInr,
                        PaymentDateLocal = p.PaymentDateLocal,
                        PaymentMethod = p.PaymentMethod,
                        PaymentMethodDisplay = FormatPaymentMethod(p.PaymentMethod),
                        ExternalTransactionRef = p.ExternalTransactionRef,
                        Notes = p.Notes,
                        RecordedByAdminName = p.RecordedByAdmin.FullName,
                        RecordedAtLocalDisplay = _tz.ToLocalTime(p.RecordedAtUtc).ToString("dd MMM yyyy, hh:mm tt") + " IST",
                        IsReversed = p.IsReversed,
                        ReversalReason = p.ReversalReason,
                        ReversedAtLocalDisplay = p.ReversedAtUtc.HasValue
                            ? _tz.ToLocalTime(p.ReversedAtUtc.Value).ToString("dd MMM yyyy, hh:mm tt") + " IST"
                            : null
                    }).ToList()
            };

            return View(model);
        }

        // ==================================================================
        // GET: /Maintenance/Receipt/{paymentId} (Printable Official Payment Receipt)
        // ==================================================================
        [HttpGet]
        public async Task<IActionResult> Receipt(int id, CancellationToken ct = default)
        {
            var isAdmin = User.IsInRole("Administrator");
            var resident = await GetVerifiedResidentProfileAsync(ct);

            if (!isAdmin && resident == null)
            {
                return View("~/Views/Visitors/UnverifiedResidentNotice.cshtml");
            }

            var payment = await _context.MaintenancePayments
                .AsNoTracking()
                .Include(p => p.RecordedByAdmin)
                .Include(p => p.Bill)
                    .ThenInclude(b => b.Flat)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (payment == null) return NotFound();

            // Strict flat ownership check
            if (!isAdmin && payment.Bill.FlatId != resident!.FlatId)
            {
                return NotFound();
            }

            var model = new PaymentReceiptViewModel
            {
                PaymentId = payment.Id,
                PaymentReference = payment.PaymentReference,
                BillId = payment.BillId,
                BillNumber = payment.Bill.BillNumber,
                BillingPeriodDisplay = FormatBillingPeriod(payment.Bill.BillingYear, payment.Bill.BillingMonth),
                FlatDisplay = $"{payment.Bill.Flat.Building.Code}-{payment.Bill.Flat.FlatNumber}",
                BuildingName = payment.Bill.Flat.Building.Name,
                AmountReceivedInr = payment.AmountReceivedInr,
                PaymentDateLocal = payment.PaymentDateLocal,
                PaymentMethodDisplay = FormatPaymentMethod(payment.PaymentMethod),
                ExternalTransactionRef = payment.ExternalTransactionRef,
                Notes = payment.Notes,
                RecordedByAdminName = payment.RecordedByAdmin.FullName,
                RecordedAtLocalDisplay = _tz.ToLocalTime(payment.RecordedAtUtc).ToString("dd MMM yyyy, hh:mm tt") + " IST",
                IsReversed = payment.IsReversed,
                ReversalReason = payment.ReversalReason
            };

            return View(model);
        }

        // ==================================================================
        // GET: /Maintenance/Admin (Administrator Billing & Collection Console)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> Admin(
            int? year,
            int? month,
            int? buildingId,
            string? statusFilter,
            CancellationToken ct = default)
        {
            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);

            var buildings = await _context.Buildings
                .AsNoTracking()
                .OrderBy(b => b.Code)
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = $"{b.Code} — {b.Name}" })
                .ToListAsync(ct);

            var query = _context.MaintenanceBills
                .AsNoTracking()
                .Include(b => b.Flat)
                    .ThenInclude(f => f.Building)
                .Include(b => b.Payments)
                .Include(b => b.Adjustments)
                .AsQueryable();

            if (year.HasValue)
            {
                query = query.Where(b => b.BillingYear == year.Value);
            }
            if (month.HasValue)
            {
                query = query.Where(b => b.BillingMonth == month.Value);
            }
            if (buildingId.HasValue)
            {
                query = query.Where(b => b.Flat.BuildingId == buildingId.Value);
            }

            var rawBills = await query
                .OrderByDescending(b => b.BillingYear)
                .ThenByDescending(b => b.BillingMonth)
                .ThenBy(b => b.Flat.Building.Code)
                .ThenBy(b => b.Flat.FlatNumber)
                .Take(200)
                .ToListAsync(ct);

            var allCards = rawBills.Select(b => MapBillToCard(b, todayLocal)).ToList();
            var activeNonVoid = allCards.Where(c => !c.IsVoid).ToList();

            var filtered = allCards.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                filtered = statusFilter.ToLowerInvariant() switch
                {
                    "unpaid" => filtered.Where(c => c.Status == MaintenanceBillStatus.Unpaid),
                    "partial" => filtered.Where(c => c.Status == MaintenanceBillStatus.PartiallyPaid),
                    "overdue" => filtered.Where(c => c.IsOverdue),
                    "paid" => filtered.Where(c => c.Status == MaintenanceBillStatus.Paid),
                    _ => filtered
                };
            }

            var model = new AdminMaintenanceDashboardViewModel
            {
                SelectedYear = year,
                SelectedMonth = month,
                SelectedBuildingId = buildingId,
                SelectedStatusFilter = statusFilter,
                TotalBilledInr = activeNonVoid.Sum(c => c.NetBillAmountInr),
                TotalCollectedInr = activeNonVoid.Sum(c => c.TotalPaidInr),
                TotalOutstandingInr = activeNonVoid.Sum(c => c.OutstandingBalanceInr),
                UnpaidBillsCount = activeNonVoid.Count(c => c.Status == MaintenanceBillStatus.Unpaid),
                PartiallyPaidBillsCount = activeNonVoid.Count(c => c.Status == MaintenanceBillStatus.PartiallyPaid),
                OverdueBillsCount = activeNonVoid.Count(c => c.IsOverdue),
                PaidBillsCount = activeNonVoid.Count(c => c.Status == MaintenanceBillStatus.Paid),
                Buildings = buildings,
                Bills = filtered.ToList()
            };

            return View(model);
        }

        // ==================================================================
        // GET & POST: /Maintenance/CreateBill (Single or Safe Bulk Generation)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> CreateBill(CancellationToken ct = default)
        {
            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);
            var model = new CreateBillsInputModel
            {
                BillingYear = todayLocal.Year,
                BillingMonth = todayLocal.Month,
                IssueDateLocal = todayLocal,
                DueDateLocal = todayLocal.AddDays(15),
                AmountInr = 3200.00m
            };

            await PopulateBuildingsAndFlatsAsync(model, ct);
            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBill(CreateBillsInputModel model, CancellationToken ct = default)
        {
            if (model.DueDateLocal < model.IssueDateLocal)
            {
                ModelState.AddModelError(nameof(model.DueDateLocal), "Due date cannot be earlier than the issue date.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateBuildingsAndFlatsAsync(model, ct);
                return View(model);
            }

            List<Flat> targetFlats;
            if (model.TargetScope == "SingleFlat")
            {
                if (!model.FlatId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.FlatId), "Please select a target flat.");
                    await PopulateBuildingsAndFlatsAsync(model, ct);
                    return View(model);
                }
                targetFlats = await _context.Flats
                    .Include(f => f.Building)
                    .Where(f => f.Id == model.FlatId.Value)
                    .ToListAsync(ct);
            }
            else if (model.TargetScope == "Building")
            {
                if (!model.BuildingId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.BuildingId), "Please select a target building.");
                    await PopulateBuildingsAndFlatsAsync(model, ct);
                    return View(model);
                }
                targetFlats = await _context.Flats
                    .Include(f => f.Building)
                    .Where(f => f.BuildingId == model.BuildingId.Value)
                    .OrderBy(f => f.FlatNumber)
                    .ToListAsync(ct);
            }
            else
            {
                targetFlats = await _context.Flats
                    .Include(f => f.Building)
                    .OrderBy(f => f.Building.Code)
                    .ThenBy(f => f.FlatNumber)
                    .ToListAsync(ct);
            }

            if (!targetFlats.Any())
            {
                ModelState.AddModelError(string.Empty, "No valid flats found for the selected target scope.");
                await PopulateBuildingsAndFlatsAsync(model, ct);
                return View(model);
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = _tz.UtcNow;

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var existingFlatIds = await _context.MaintenanceBills
                .Where(b => b.BillingYear == model.BillingYear && b.BillingMonth == model.BillingMonth)
                .Select(b => b.FlatId)
                .ToHashSetAsync(ct);

            int createdCount = 0;
            int skippedCount = 0;

            foreach (var flat in targetFlats)
            {
                if (existingFlatIds.Contains(flat.Id))
                {
                    skippedCount++;
                    continue;
                }

                var billNumber = $"BILL-{model.BillingYear}{model.BillingMonth:D2}-{flat.Building.Code}{flat.FlatNumber}";
                _context.MaintenanceBills.Add(new MaintenanceBill
                {
                    BillNumber = billNumber,
                    FlatId = flat.Id,
                    BillingYear = model.BillingYear,
                    BillingMonth = model.BillingMonth,
                    IssueDateLocal = model.IssueDateLocal,
                    DueDateLocal = model.DueDateLocal,
                    Description = model.Description.Trim(),
                    OriginalAmountInr = decimal.Round(model.AmountInr, 2),
                    CreatedByAdminId = adminId,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc
                });
                createdCount++;
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            if (createdCount == 0 && skippedCount > 0)
            {
                TempData["WarningMessage"] = $"No new bills were created because all {skippedCount} selected flat(s) already have a bill for {FormatBillingPeriod(model.BillingYear, model.BillingMonth)}.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Generated {createdCount} maintenance bill(s) for {FormatBillingPeriod(model.BillingYear, model.BillingMonth)}" +
                    (skippedCount > 0 ? $" ({skippedCount} existing duplicate flat(s) safely skipped)." : ".");
            }

            return RedirectToAction(nameof(Admin), new { year = model.BillingYear, month = model.BillingMonth });
        }

        // ==================================================================
        // GET & POST: /Maintenance/RecordPayment/{id} (Concurrency-Safe Ledger Entry)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> RecordPayment(int id, CancellationToken ct = default)
        {
            var bill = await _context.MaintenanceBills
                .AsNoTracking()
                .Include(b => b.Flat)
                    .ThenInclude(f => f.Building)
                .Include(b => b.Payments)
                .Include(b => b.Adjustments)
                .FirstOrDefaultAsync(b => b.Id == id, ct);

            if (bill == null) return NotFound();

            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);
            var card = MapBillToCard(bill, todayLocal);

            if (card.IsVoid || card.OutstandingBalanceInr <= 0m)
            {
                TempData["WarningMessage"] = "This bill is already fully settled or voided.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new RecordPaymentInputModel
            {
                BillId = bill.Id,
                BillNumber = bill.BillNumber,
                FlatDisplay = card.FlatDisplay,
                BillingPeriodDisplay = card.BillingPeriodDisplay,
                NetBillAmountInr = card.NetBillAmountInr,
                AlreadyPaidInr = card.TotalPaidInr,
                CurrentOutstandingInr = card.OutstandingBalanceInr,
                AmountReceivedInr = card.OutstandingBalanceInr,
                PaymentDateLocal = todayLocal
            };

            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(RecordPaymentInputModel input, CancellationToken ct = default)
        {
            var nowUtc = _tz.UtcNow;
            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Acquire an exclusive row lock (UPDLOCK, HOLDLOCK) on the parent MaintenanceBill row
            // so two concurrent payment submissions cannot both consume the same remaining balance.
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM MaintenanceBills WITH (UPDLOCK, HOLDLOCK) WHERE Id = {0}",
                    new object[] { input.BillId },
                    ct);

                var bill = await _context.MaintenanceBills
                    .Include(b => b.Flat)
                        .ThenInclude(f => f.Building)
                    .Include(b => b.Payments)
                    .Include(b => b.Adjustments)
                    .FirstOrDefaultAsync(b => b.Id == input.BillId, ct);

                if (bill == null)
                {
                    await tx.RollbackAsync(ct);
                    return NotFound();
                }

                var card = MapBillToCard(bill, todayLocal);

                if (bill.IsVoid)
                {
                    await tx.RollbackAsync(ct);
                    TempData["ErrorMessage"] = "Cannot record a payment against a voided bill.";
                    return RedirectToAction(nameof(Details), new { id = bill.Id });
                }

                var requestedAmount = decimal.Round(input.AmountReceivedInr, 2);
                if (requestedAmount <= 0m)
                {
                    await tx.RollbackAsync(ct);
                    TempData["ErrorMessage"] = "Payment amount must be greater than ₹0.";
                    return RedirectToAction(nameof(RecordPayment), new { id = bill.Id });
                }

                if (requestedAmount > card.OutstandingBalanceInr)
                {
                    await tx.RollbackAsync(ct);
                    TempData["ErrorMessage"] = $"Overpayment rejected: ₹{requestedAmount:N2} exceeds the remaining outstanding balance of ₹{card.OutstandingBalanceInr:N2}.";
                    return RedirectToAction(nameof(RecordPayment), new { id = bill.Id });
                }

                // Rapid duplicate submission guard (within 60 seconds)
                var cleanExtRef = input.ExternalTransactionRef?.Trim();
                var isRapidDuplicate = bill.Payments.Any(p =>
                    !p.IsReversed &&
                    p.AmountReceivedInr == requestedAmount &&
                    p.PaymentMethod == input.PaymentMethod &&
                    string.Equals(p.ExternalTransactionRef, cleanExtRef, StringComparison.OrdinalIgnoreCase) &&
                    p.RecordedAtUtc >= nowUtc.AddSeconds(-60));

                if (isRapidDuplicate)
                {
                    await tx.RollbackAsync(ct);
                    TempData["WarningMessage"] = "An identical payment for this bill was just recorded moments ago.";
                    return RedirectToAction(nameof(Details), new { id = bill.Id });
                }

                var randomSuffix = RandomNumberGenerator.GetInt32(1000, 9999);
                var paymentRef = $"RCPT-{nowUtc:yyyyMMdd}-{randomSuffix}";

                var payment = new MaintenancePayment
                {
                    BillId = bill.Id,
                    PaymentReference = paymentRef,
                    AmountReceivedInr = requestedAmount,
                    PaymentDateLocal = input.PaymentDateLocal,
                    PaymentMethod = input.PaymentMethod,
                    ExternalTransactionRef = cleanExtRef,
                    Notes = input.Notes?.Trim(),
                    RecordedByAdminId = adminId,
                    RecordedAtUtc = nowUtc
                };

                _context.MaintenancePayments.Add(payment);
                bill.UpdatedAtUtc = nowUtc;

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["SuccessMessage"] = $"Recorded payment {payment.PaymentReference} of {FormatInr(requestedAmount)}.";
                return RedirectToAction(nameof(Receipt), new { id = payment.Id });
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                TempData["ErrorMessage"] = "Unable to record the payment due to a concurrent update. Please verify the balance and try again.";
                return RedirectToAction(nameof(Details), new { id = input.BillId });
            }
        }

        // ==================================================================
        // POST: /Maintenance/AddAdjustment (Audited Credit, Waiver, or Charge)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdjustment(AddAdjustmentInputModel input, CancellationToken ct = default)
        {
            if (!ModelState.IsValid || input.AmountInr <= 0m)
            {
                TempData["ErrorMessage"] = "Please provide a valid positive adjustment amount and reason.";
                return RedirectToAction(nameof(Details), new { id = input.BillId });
            }

            var todayLocal = DateOnly.FromDateTime(_tz.NowLocal);
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var nowUtc = _tz.UtcNow;

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT Id FROM MaintenanceBills WITH (UPDLOCK, HOLDLOCK) WHERE Id = {0}",
                new object[] { input.BillId },
                ct);

            var bill = await _context.MaintenanceBills
                .Include(b => b.Flat)
                    .ThenInclude(f => f.Building)
                .Include(b => b.Payments)
                .Include(b => b.Adjustments)
                .FirstOrDefaultAsync(b => b.Id == input.BillId, ct);

            if (bill == null) return NotFound();

            var card = MapBillToCard(bill, todayLocal);
            var amount = decimal.Round(input.AmountInr, 2);

            if (input.Type == MaintenanceAdjustmentType.CreditOrWaiver && amount > card.OutstandingBalanceInr)
            {
                await tx.RollbackAsync(ct);
                TempData["ErrorMessage"] = $"Credit/waiver of {FormatInr(amount)} cannot exceed the current unpaid balance of {FormatInr(card.OutstandingBalanceInr)}.";
                return RedirectToAction(nameof(Details), new { id = input.BillId });
            }

            _context.MaintenanceAdjustments.Add(new MaintenanceAdjustment
            {
                BillId = bill.Id,
                Type = input.Type,
                AmountInr = amount,
                Reason = input.Reason.Trim(),
                CreatedByAdminId = adminId,
                CreatedAtUtc = nowUtc
            });

            bill.UpdatedAtUtc = nowUtc;
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            TempData["SuccessMessage"] = $"Recorded {input.Type} of {FormatInr(amount)}.";
            return RedirectToAction(nameof(Details), new { id = input.BillId });
        }

        // ==================================================================
        // POST: /Maintenance/ReversePayment (Auditable Payment Reversal)
        // ==================================================================
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReversePayment(int paymentId, string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            {
                TempData["ErrorMessage"] = "A reversal reason (at least 5 characters) is mandatory for audit integrity.";
                return RedirectToAction(nameof(Admin));
            }

            var payment = await _context.MaintenancePayments
                .Include(p => p.Bill)
                .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

            if (payment == null) return NotFound();

            if (payment.IsReversed)
            {
                TempData["WarningMessage"] = "This payment has already been reversed.";
                return RedirectToAction(nameof(Details), new { id = payment.BillId });
            }

            var nowUtc = _tz.UtcNow;
            payment.IsReversed = true;
            payment.ReversedAtUtc = nowUtc;
            payment.ReversedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            payment.ReversalReason = reason.Trim();
            payment.Bill.UpdatedAtUtc = nowUtc;

            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = $"Payment {payment.PaymentReference} reversed. The bill's outstanding balance has been restored.";
            return RedirectToAction(nameof(Details), new { id = payment.BillId });
        }

        private async Task PopulateBuildingsAndFlatsAsync(CreateBillsInputModel model, CancellationToken ct)
        {
            var buildings = await _context.Buildings
                .AsNoTracking()
                .OrderBy(b => b.Code)
                .ToListAsync(ct);

            model.Buildings = buildings.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = $"{b.Code} — {b.Name}"
            }).ToList();

            var flats = await _context.Flats
                .AsNoTracking()
                .Include(f => f.Building)
                .OrderBy(f => f.Building.Code)
                .ThenBy(f => f.Floor)
                .ThenBy(f => f.FlatNumber)
                .ToListAsync(ct);

            model.Flats = flats.Select(f => new SelectListItem
            {
                Value = f.Id.ToString(),
                Text = $"Flat {f.Building.Code}-{f.FlatNumber} ({f.Building.Name})"
            }).ToList();
        }
    }
}