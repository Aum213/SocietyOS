using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocietyOS.Models
{
    public enum MaintenanceBillStatus
    {
        Unpaid = 0,
        PartiallyPaid = 1,
        Paid = 2,
        Void = 3
    }

    public enum MaintenancePaymentMethod
    {
        BankTransferNeftImps = 0,
        UpiTransfer = 1,
        Cheque = 2,
        Cash = 3,
        Other = 4
    }

    public enum MaintenanceAdjustmentType
    {
        CreditOrWaiver = 0,
        AdditionalCharge = 1
    }

    public class MaintenanceBill
    {
        public int Id { get; set; }

        [Required, MaxLength(40)]
        public string BillNumber { get; set; } = string.Empty; // e.g. "BILL-202610-B204"

        public int FlatId { get; set; }
        public virtual Flat Flat { get; set; } = null!;

        [Range(2020, 2100)]
        public int BillingYear { get; set; }

        [Range(1, 12)]
        public int BillingMonth { get; set; }

        public DateOnly IssueDateLocal { get; set; }
        public DateOnly DueDateLocal { get; set; }

        [Required, MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalAmountInr { get; set; }

        public bool IsVoid { get; set; } = false;

        [MaxLength(300)]
        public string? VoidReason { get; set; }
        public DateTime? VoidedAtUtc { get; set; }
        public string? VoidedByAdminId { get; set; }
        public virtual ApplicationUser? VoidedByAdmin { get; set; }

        [Required]
        public string CreatedByAdminId { get; set; } = string.Empty;
        public virtual ApplicationUser CreatedByAdmin { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public virtual ICollection<MaintenancePayment> Payments { get; set; } = new List<MaintenancePayment>();
        public virtual ICollection<MaintenanceAdjustment> Adjustments { get; set; } = new List<MaintenanceAdjustment>();
    }

    public class MaintenancePayment
    {
        public int Id { get; set; }

        public int BillId { get; set; }
        public virtual MaintenanceBill Bill { get; set; } = null!;

        [Required, MaxLength(40)]
        public string PaymentReference { get; set; } = string.Empty; // e.g. "RCPT-20261025-8392"

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountReceivedInr { get; set; }

        public DateOnly PaymentDateLocal { get; set; }

        public MaintenancePaymentMethod PaymentMethod { get; set; } = MaintenancePaymentMethod.UpiTransfer;

        [MaxLength(100)]
        public string? ExternalTransactionRef { get; set; }

        [MaxLength(300)]
        public string? Notes { get; set; }

        [Required]
        public string RecordedByAdminId { get; set; } = string.Empty;
        public virtual ApplicationUser RecordedByAdmin { get; set; } = null!;

        public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

        // Immutable reversal audit fields
        public bool IsReversed { get; set; } = false;
        public DateTime? ReversedAtUtc { get; set; }
        public string? ReversedByAdminId { get; set; }
        public virtual ApplicationUser? ReversedByAdmin { get; set; }

        [MaxLength(300)]
        public string? ReversalReason { get; set; }
    }

    public class MaintenanceAdjustment
    {
        public int Id { get; set; }

        public int BillId { get; set; }
        public virtual MaintenanceBill Bill { get; set; } = null!;

        public MaintenanceAdjustmentType Type { get; set; } = MaintenanceAdjustmentType.CreditOrWaiver;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountInr { get; set; }

        [Required, MaxLength(300)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string CreatedByAdminId { get; set; } = string.Empty;
        public virtual ApplicationUser CreatedByAdmin { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}