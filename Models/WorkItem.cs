namespace ArchtApp.Api.Models;

public class WorkItem
{
    public int WorkItemId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Type { get; set; } = "task"; // task, site_visit, consultation
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // For tasks this is the due date. For appointments it is the start date and time.
    public DateTime? DueDate { get; set; }

    // Appointment details
    public int? DurationMinutes { get; set; }
    public string? Address { get; set; }
    public int? VisitNumber { get; set; } // 1, 2, 3... per project, site visits only
    public string? GoogleEventId { get; set; }

    public bool IsDone { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}