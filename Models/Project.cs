using System.Reflection.Metadata;

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

    public List<ProjectTrack> Tracks { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
    public List<ActivityLog> ActivityLogs { get; set; } = new();
}