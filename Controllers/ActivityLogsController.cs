using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreateNoteRequest(int ProjectId, string Description);

[ApiController]
[Route("api/[controller]")]
public class ActivityLogsController : ControllerBase
{
    private readonly ArchtAppDbContext _db;

    public ActivityLogsController(ArchtAppDbContext db) => _db = db;

    // GET api/activitylogs/feed?limit=200 -> newest first, across all projects
    [HttpGet("feed")]
    public async Task<IActionResult> Feed([FromQuery] int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 500);

        var items = await _db.ActivityLogs.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .Select(l => new
            {
                l.ActivityLogId,
                l.ProjectId,
                projectName = l.Project!.ProjectName,
                clientName = l.Project.Client!.Name,
                l.EventType,
                l.Description,
                l.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET api/activitylogs?projectId=1 -> newest first
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityLog>>> GetAll([FromQuery] int? projectId)
    {
        var query = _db.ActivityLogs.AsNoTracking().AsQueryable();
        if (projectId.HasValue) query = query.Where(l => l.ProjectId == projectId.Value);
        return await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
    }

    // The only log entries typed by hand are notes; everything else is written automatically
    [HttpPost("note")]
    public async Task<ActionResult<ActivityLog>> AddNote(CreateNoteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Description))
            return BadRequest("Note text is required.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
            return BadRequest($"Project {req.ProjectId} does not exist.");

        var entry = new ActivityLog
        {
            ProjectId = req.ProjectId,
            EventType = "note",
            Description = req.Description.Trim()
        };

        _db.ActivityLogs.Add(entry);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { projectId = entry.ProjectId }, entry);
    }
}