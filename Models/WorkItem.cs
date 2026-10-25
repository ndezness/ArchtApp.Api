namespace ArchtApp.Api.Models;

public class WorkItem
{
    public int WorkItemId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Type { get; set; } = "task"; // task, site_visit
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsDone { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}