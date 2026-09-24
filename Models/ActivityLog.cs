namespace ArchtApp.Api.Models;

public class ActivityLog
{
    public int LogId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string EventType { get; set; } = string.Empty; // stage_change, payment_received, document_uploaded, note
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}