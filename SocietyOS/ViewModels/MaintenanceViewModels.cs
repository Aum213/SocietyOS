using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class ResidentMaintenanceDashboardViewModel
    {
        public string FlatDisplay { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;

        public decimal TotalOutstandingInr { get; set; }
        public int UnpaidOrPartialCount { get; set; }
        public int OverdueCount { get; set; }
        public int PaidCount { get; set; }

        public int? SelectedYear { get; set; }
        public string? SelectedStatusFilter { get; set; } // "open", "overdue", "paid"

        public List<MaintenanceBillCardViewModel> Bills { get; set; } = new();
    }

    public class MaintenanceBillCardViewModel
    {
        public int Id { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public string FlatDisplay { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public int BillingYear { get; set; }
        public int BillingMonth { get; set; }
        public string BillingPeriodDisplay { get; set; } = string.Empty;
        public DateOnly IssueDateLocal { get; set; }
        public DateOnly DueDateLocal { get; set; }
        public string Description { get; set; } = string.Empty;

        public decimal OriginalAmountInr { get; set; }
        public decimal NetAdjustmentsInr { get; set; }
        public decimal NetBillAmountInr { get; set; }
        public decimal TotalPaidInr { get; set; }
        public decimal OutstandingBalanceInr { get; set; }

        public MaintenanceBillStatus Status { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsVoid { get; set; }
    }

    public class MaintenanceBillDetailsViewModel
    {
        public MaintenanceBillCardViewModel Bill { get; set; } = new();
        public string? VoidReason { get; set; }
        public bool IsAdmin { get; set; }

        public List<MaintenanceAdjustmentItemViewModel> Adjustments { get; set; } = new();
        public List<MaintenancePaymentItemViewModel> Payments { get; set; } = new();
    }

    public class MaintenanceAdjustmentItemViewModel
    {
        public int Id { get; set; }
        public MaintenanceAdjustmentType Type { get; set; }
        public decimal AmountInr { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string CreatedAtLocalDisplay { get; set; } = string.Empty;
    }

    public class MaintenancePaymentItemViewModel
    {
        public int Id { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public decimal AmountReceivedInr { get; set; }
        public DateOnly PaymentDateLocal { get; set; }
        public MaintenancePaymentMethod PaymentMethod { get; set; }
        public string PaymentMethodDisplay { get; set; } = string.Empty;
        public string? ExternalTransactionRef { get; set; }
        public string? Notes { get; set; }
        public string RecordedByAdminName { get; set; } = string.Empty;
        public string RecordedAtLocalDisplay { get; set; } = string.Empty;

        public bool IsReversed { get; set; }
        public string? ReversalReason { get; set; }
        public string? ReversedAtLocalDisplay { get; set; }
    }

    public class PaymentReceiptViewModel
    {
        public int PaymentId { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public int BillId { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public string BillingPeriodDisplay { get; set; } = string.Empty;
        public string FlatDisplay { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public decimal AmountReceivedInr { get; set; }
        public DateOnly PaymentDateLocal { get; set; }
        public string PaymentMethodDisplay { get; set; } = string.Empty;
        public string? ExternalTransactionRef { get; set; }
        public string? Notes { get; set; }
        public string RecordedByAdminName { get; set; } = string.Empty;
        public string RecordedAtLocalDisplay { get; set; } = string.Empty;
        public bool IsReversed { get; set; }
        public string? ReversalReason { get; set; }
    }

    public class AdminMaintenanceDashboardViewModel
    {
        public int? SelectedYear { get; set; }
        public int? SelectedMonth { get; set; }
        public int? SelectedBuildingId { get; set; }
        public string? SelectedStatusFilter { get; set; }

        public decimal TotalBilledInr { get; set; }
        public decimal TotalCollectedInr { get; set; }
        public decimal TotalOutstandingInr { get; set; }
        public int UnpaidBillsCount { get; set; }
        public int PartiallyPaidBillsCount { get; set; }
        public int OverdueBillsCount { get; set; }
        public int PaidBillsCount { get; set; }

        public List<SelectListItem> Buildings { get; set; } = new();
        public List<MaintenanceBillCardViewModel> Bills { get; set; } = new();
    }

    public class CreateBillsInputModel
    {
        [Required]
        [Display(Name = "Generation Target")]
        public string TargetScope { get; set; } = "SingleFlat"; // "SingleFlat", "Building", "AllSociety"

        [Display(Name = "Building / Tower")]
        public int? BuildingId { get; set; }

        [Display(Name = "Specific Flat")]
        public int? FlatId { get; set; }

        [Required, Range(2024, 2035)]
        [Display(Name = "Billing Year")]
        public int BillingYear { get; set; }

        [Required, Range(1, 12)]
        [Display(Name = "Billing Month")]
        public int BillingMonth { get; set; }

        [Required]
        [Display(Name = "Issue Date (IST)")]
        public DateOnly IssueDateLocal { get; set; }

        [Required]
        [Display(Name = "Due Date (IST)")]
        public DateOnly DueDateLocal { get; set; }

        [Required(ErrorMessage = "Please enter a valid positive bill amount.")]
        [Range(1, 500000, ErrorMessage = "Bill amount must be between ₹1 and ₹5,00,000.")]
        [Display(Name = "Monthly Maintenance Amount per Flat (₹ INR)")]
        public decimal AmountInr { get; set; } = 3200.00m;

        [Required, StringLength(300, MinimumLength = 5)]
        [Display(Name = "Billing Description")]
        public string Description { get; set; } = "Monthly Society Maintenance & Common Area Charges";

        public List<SelectListItem> Buildings { get; set; } = new();
        public List<SelectListItem> Flats { get; set; } = new();
    }

    public class RecordPaymentInputModel
    {
        [Required]
        public int BillId { get; set; }

        public string BillNumber { get; set; } = string.Empty;
        public string FlatDisplay { get; set; } = string.Empty;
        public string BillingPeriodDisplay { get; set; } = string.Empty;
        public decimal NetBillAmountInr { get; set; }
        public decimal AlreadyPaidInr { get; set; }
        public decimal CurrentOutstandingInr { get; set; }

        [Required(ErrorMessage = "Please enter the payment amount received.")]
        [Range(0.01, 500000, ErrorMessage = "Payment amount must be greater than ₹0.")]
        [Display(Name = "Amount Received (₹ INR)")]
        public decimal AmountReceivedInr { get; set; }

        [Required]
        [Display(Name = "Payment Date (IST)")]
        public DateOnly PaymentDateLocal { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public MaintenancePaymentMethod PaymentMethod { get; set; } = MaintenancePaymentMethod.UpiTransfer;

        [StringLength(100)]
        [Display(Name = "External Transaction / Cheque Ref (Optional)")]
        public string? ExternalTransactionRef { get; set; }

        [StringLength(300)]
        [Display(Name = "Admin Notes (Optional)")]
        public string? Notes { get; set; }
    }

    public class AddAdjustmentInputModel
    {
        [Required]
        public int BillId { get; set; }

        [Required]
        public MaintenanceAdjustmentType Type { get; set; } = MaintenanceAdjustmentType.CreditOrWaiver;

        [Required]
        [Range(1, 500000, ErrorMessage = "Adjustment amount must be greater than ₹0.")]
        public decimal AmountInr { get; set; }

        [Required, StringLength(300, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }
}