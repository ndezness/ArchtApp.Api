namespace ArchtApp.Api.Models;

public class Project
{
    public int ProjectId { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime? StartDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public decimal? TotalQuotedCost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // e.g. BDL-2026-001 (empty on older records)
    public string? Reference { get; set; }
    public string? SiteAddress { get; set; }
    public string? ProjectInterest { get; set; }

    // Milestones from the firm's process
    public int? DesignWorkingDays { get; set; }
    public DateTime? DesignDueDate { get; set; }
    public DateTime? DesignApprovedOn { get; set; }
    public string? PmcOutcome { get; set; } // accepted, corrections_required
    public DateTime? PermitSubmittedOn { get; set; }
    public DateTime? PermitCollectedOn { get; set; }

    public List<ProjectTrack> Tracks { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
    public List<ActivityLog> ActivityLogs { get; set; } = new();
}