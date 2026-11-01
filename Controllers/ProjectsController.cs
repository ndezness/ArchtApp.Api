using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record UpdateProjectRequest(
    string ProjectName, decimal? TotalQuotedCost, DateTime? StartDate, DateTime? TargetCompletionDate);
public record ChangeProjectStatusRequest(string Status);

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ArchtAppDbContext _db;
    public ProjectsController(ArchtAppDbContext db) => _db = db;
    private static readonly string[] ValidStatuses = { "active", "on_hold", "completed", "cancelled" };
    private static string Money(decimal? v) => v.HasValue ? v.Value.ToString("N2") : "none";
    private static string Day(DateTime? v) => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : "none";

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> GetAll() =>
        await _db.Projects
            .AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.Tracks).ThenInclude(t => t.CurrentStage)
            .OrderBy(p => p.ProjectId)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Project>> Get(int id)
    {
        var project = await _db.Projects
            .Include(p => p.Client)
            .Include(p => p.Tracks).ThenInclude(t => t.CurrentStage)
            .Include(p => p.Payments)
            .Include(p => p.Documents)
            .Include(p => p.ActivityLogs)
            .FirstOrDefaultAsync(p => p.ProjectId == id);

        return project is null ? NotFound() : project;
    }

    [HttpPost]
    public async Task<ActionResult<Project>> Create(Project project)
    {
        if (!await _db.Clients.AnyAsync(c => c.ClientId == project.ClientId))
            return BadRequest($"Client {project.ClientId} does not exist.");

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = project.ProjectId }, project);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProjectRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProjectName))
            return BadRequest("Project name is required.");
        if (req.TotalQuotedCost is < 0)
            return BadRequest("Quoted cost can't be negative.");

        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        var changes = new List<string>();
        var name = req.ProjectName.Trim();

        if (name != project.ProjectName)
        {
            changes.Add($"name changed from '{project.ProjectName}' to '{name}'");
            project.ProjectName = name;
        }
        if (req.TotalQuotedCost != project.TotalQuotedCost)
        {
            changes.Add($"quoted cost changed from {Money(project.TotalQuotedCost)} to {Money(req.TotalQuotedCost)}");
            project.TotalQuotedCost = req.TotalQuotedCost;
        }
        if (req.StartDate?.Date != project.StartDate?.Date)
        {
            changes.Add($"start date changed from {Day(project.StartDate)} to {Day(req.StartDate)}");
            project.StartDate = req.StartDate;
        }
        if (req.TargetCompletionDate?.Date != project.TargetCompletionDate?.Date)
        {
            changes.Add($"target date changed from {Day(project.TargetCompletionDate)} to {Day(req.TargetCompletionDate)}");
            project.TargetCompletionDate = req.TargetCompletionDate;
        }

        if (changes.Count > 0)
        {
            _db.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = id,
                EventType = "project_updated",
                Description = $"Project details updated: {string.Join("; ", changes)}."
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ChangeProjectStatusRequest req)
    {
        var status = req.Status?.Trim().ToLowerInvariant();
        if (status is null || !ValidStatuses.Contains(status))
            return BadRequest("Status must be one of: active, on_hold, completed, cancelled.");

        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        if (project.Status == status) return NoContent();

        var old = project.Status;
        project.Status = status;

        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = id,
            EventType = "project_status_change",
            Description = $"Project status changed from '{old}' to '{status}'."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}