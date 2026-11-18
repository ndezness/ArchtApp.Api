namespace ArchtApp.Api.Models;

public class Lead
{
    public int LeadId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string SiteAddress { get; set; } = string.Empty;
    public string? ProjectInterest { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "open"; // open, converted, lost
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? ConvertedProjectId { get; set; }
}