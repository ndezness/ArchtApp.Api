using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreateTrackRequest(int ProjectId, string TrackName, int StageId);
public record ChangeStageRequest(int StageId);
public record ChangeStatusRequest(string Status);

[ApiController]
[Route("api/[controller]")]
public class ProjectTracksController : ControllerBase
{
    private static readonly string[] ValidStatuses = { "active", "blocked", "completed" };
    private readonly ArchtAppDbContext _db;

    public ProjectTracksController(ArchtAppDbContext db) => _db = db;

    // GET api/projecttracks            -> all tracks
    // GET api/projecttracks?projectId=3 -> tracks for one project
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectTrack>>> GetAll([FromQuery] int? projectId)
    {
        var query = _db.ProjectTracks.AsNoTracking().Include(t => t.CurrentStage).AsQueryable();
        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
        return await query.OrderBy(t => t.ProjectTrackId).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectTrack>> Get(int id)
    {
        var track = await _db.ProjectTracks
            .Include(t => t.CurrentStage)
            .FirstOrDefaultAsync(t => t.ProjectTrackId == id);
        return track is null ? NotFound() : track;
    }

    [HttpPost]
    public async Task<ActionResult<ProjectTrack>> Create(CreateTrackRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TrackName))
            return BadRequest("Track name is required.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
            return BadRequest($"Project {req.ProjectId} does not exist.");

        var stage = await _db.Stages.FindAsync(req.StageId);
        if (stage is null)
            return BadRequest($"Stage {req.StageId} does not exist.");

        var track = new ProjectTrack
        {
            ProjectId = req.ProjectId,
            TrackName = req.TrackName.Trim(),
            CurrentStageId = req.StageId
        };

        _db.ProjectTracks.Add(track);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = req.ProjectId,
            EventType = "track_created",
            Description = $"Track '{track.TrackName}' started at stage '{stage.Name}'."
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = track.ProjectTrackId }, track);
    }

    // PUT api/projecttracks/5/stage   body: { "stageId": 3 }
    [HttpPut("{id}/stage")]
    public async Task<IActionResult> ChangeStage(int id, ChangeStageRequest req)
    {
        var track = await _db.ProjectTracks.FindAsync(id);
        if (track is null) return NotFound();

        var newStage = await _db.Stages.FindAsync(req.StageId);
        if (newStage is null) return BadRequest($"Stage {req.StageId} does not exist.");

        if (track.CurrentStageId == req.StageId) return NoContent();

        var oldStage = await _db.Stages.FindAsync(track.CurrentStageId);
        track.CurrentStageId = newStage.StageId;

        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = track.ProjectId,
            EventType = "stage_change",
            Description = $"Track '{track.TrackName}' moved from '{oldStage?.Name}' to '{newStage.Name}'."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/projecttracks/5/status   body: { "status": "blocked" }
    [HttpPut("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusRequest req)
    {
        var status = req.Status?.Trim().ToLowerInvariant();
        if (status is null || !ValidStatuses.Contains(status))
            return BadRequest("Status must be one of: active, blocked, completed.");

        var track = await _db.ProjectTracks.FindAsync(id);
        if (track is null) return NotFound();

        if (track.Status == status) return NoContent();

        var oldStatus = track.Status;
        track.Status = status;
        track.CompletedAt = status == "completed" ? DateTime.UtcNow : null;

        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = track.ProjectId,
            EventType = "status_change",
            Description = $"Track '{track.TrackName}' status changed from '{oldStatus}' to '{status}'."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }
}