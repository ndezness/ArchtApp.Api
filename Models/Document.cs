namespace ArchtApp.Api.Models;

public class Document
{
    public int DocumentId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? FilePath { get; set; }
}