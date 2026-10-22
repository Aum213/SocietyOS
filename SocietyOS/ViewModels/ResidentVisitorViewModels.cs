using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SocietyOS.Models;

namespace SocietyOS.ViewModels
{
    public class ResidentVisitorInboxViewModel
    {
        public string FlatDisplay { get; set; } = string.Empty; // e.g. "B-204"
        public string BuildingName { get; set; } = string.Empty;
        public string ActiveTab { get; set; } = "Pending"; // Pending, Approved, Rejected

        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }

        public List<VisitorCardViewModel> Requests { get; set; } = new();
    }

    public class VisitorCardViewModel
    {
        public int Id { get; set; }
        public string VisitorName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string? PurposeDetails { get; set; }
        public int AccompanyingPersons { get; set; }
        public VisitorStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? DecisionAtUtc { get; set; }
        public string? DecidedBy { get; set; }
        public string? RegisteredByGuardName { get; set; }
    }

    public class VisitorDecisionInputModel
    {
        [Required]
        public int RequestId { get; set; }

        [Required]
        public string Decision { get; set; } = string.Empty; // "Approve" or "Reject"

        [MaxLength(200)]
        public string? DecisionNotes { get; set; }
    }
}