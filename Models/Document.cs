using System.Text.Json.Serialization;

namespace ArchtApp.Api.Models;

public class Document
{
    public int DocumentId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Older, name-only records keep using this
    public string? FilePath { get; set; }

    // The random name the file has on disk. Never sent to the browser.
    [JsonIgnore]
    public string? StoredFileName { get; set; }

    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }

    // Calculated, not a database column
    public bool HasFile => StoredFileName != null;
}