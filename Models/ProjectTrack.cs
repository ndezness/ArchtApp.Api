namespace ArchtApp.Api.Models;

public class ProjectTrack
{
    public int TrackId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public int CurrentStageId { get; set; }
    public Stage? CurrentStage { get; set; }
    public string Status { get; set; } = "active";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}