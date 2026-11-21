using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using ArchtApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreateTrackRequest(int ProjectId, string TrackName, int StageId);
public record ChangeStageRequest(
    int StageId, DateTime? Date, string? Note, int? WorkingDays,
    decimal? PaymentAmount, bool? CompleteProject);
public record ChangeStatusRequest(string Status);

[ApiController]
[Route("api/[controller]")]
public class ProjectTracksController : ControllerBase
{
    // The seeded stage IDs (see ArchtAppDbContext). IDs never change, even if a stage is renamed.
    private const int PreliminaryDesign = 3, DesignApproval = 5, BuildingPlans = 6, PmcReview = 7,
        PermitSubmission = 8, PermitCollection = 9, ProjectCompletion = 10;

    private static readonly string[] ValidStatuses = { "active", "blocked", "completed" };

    private readonly ArchtAppDbContext _db;
    private readonly NumberingService _numbering;

    public ProjectTracksController(ArchtAppDbContext db, NumberingService numbering)
    {
        _db = db;
        _numbering = numbering;
    }

    // Counts Monday-Friday only. Public holidays are not excluded.
    private static DateTime AddWorkingDays(DateTime start, int days)
    {
        var d = start.Date;
        var added = 0;
        while (added < days)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) added++;
        }
        return d;
    }

    // GET api/projecttracks            -> all tracks
    // GET api/projecttracks?projectId=3 -> tracks for one project
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectTrack>>> GetAll([FromQuery] int? projectId)
    {
        var query = _db.ProjectTracks.AsNoTracking().Include(t => t.CurrentStage).AsQueryable();
        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
        return await query.OrderBy(t => t.ProjectTrackId).ToListAsync();
    }

    [HttpGet("{id:int}")]
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

    // PUT api/projecttracks/5/stage
    // body: { "stageId": 3, "date": "2026-11-20", "note": "...", "workingDays": 10,
    //         "paymentAmount": 50000, "completeProject": true }
    // Everything except stageId is optional; which extras apply depends on the stage.
    [HttpPut("{id:int}/stage")]
    public async Task<IActionResult> ChangeStage(int id, ChangeStageRequest req)
    {
        var track = await _db.ProjectTracks.FindAsync(id);
        if (track is null) return NotFound();

        var newStage = await _db.Stages.FindAsync(req.StageId);
        if (newStage is null) return BadRequest($"Stage {req.StageId} does not exist.");

        if (track.CurrentStageId == req.StageId) return NoContent();

        var project = await _db.Projects.FindAsync(track.ProjectId);
        if (project is null) return NotFound();

        if (req.StageId == PreliminaryDesign && (req.WorkingDays is null or < 1 or > 365))
            return BadRequest("Enter how many Monday-Friday working days the preliminary design should take (1-365).");

        if (req.PaymentAmount.HasValue)
        {
            if (req.StageId != DesignApproval && req.StageId != ProjectCompletion)
                return BadRequest("A payment can only be recorded when moving to Design Approval or Project Completion.");
            if (req.PaymentAmount <= 0)
                return BadRequest("The payment must be greater than zero.");
        }

        var when = req.Date ?? DateTime.Now;
        var oldStage = await _db.Stages.FindAsync(track.CurrentStageId);
        var oldId = track.CurrentStageId;
        var details = new List<string>();

        track.CurrentStageId = newStage.StageId;

        switch (req.StageId)
        {
            case PreliminaryDesign:
                project.DesignWorkingDays = req.WorkingDays;
                project.DesignDueDate = AddWorkingDays(when, req.WorkingDays!.Value);
                details.Add($"{req.WorkingDays} working days Mon-Fri, due {project.DesignDueDate:yyyy-MM-dd}");
                break;
            case DesignApproval:
                project.DesignApprovedOn = when.Date;
                details.Add($"approved on {when:yyyy-MM-dd}");
                break;
            case PermitSubmission:
                project.PermitSubmittedOn = when.Date;
                details.Add($"submitted on {when:yyyy-MM-dd}");
                break;
            case PermitCollection:
                project.PermitCollectedOn = when.Date;
                details.Add($"collected on {when:yyyy-MM-dd}");
                break;
        }

        // The PMC outcome follows from where the plans go next
        if (oldId == PmcReview && req.StageId == BuildingPlans)
        {
            project.PmcOutcome = "corrections_required";
            details.Add("PMC review: corrections required");
        }
        else if (oldId == PmcReview && req.StageId == PermitSubmission)
        {
            project.PmcOutcome = "accepted";
            details.Add("PMC review: plans accepted");
        }

        var text = $"Track '{track.TrackName}' moved from '{oldStage?.Name}' to '{newStage.Name}'";
        if (details.Count > 0) text += $" ({string.Join("; ", details)})";
        text += ".";
        if (!string.IsNullOrWhiteSpace(req.Note)) text += $" Note: {req.Note.Trim()}";

        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = track.ProjectId,
            EventType = "stage_change",
            Description = text
        });

        if (req.PaymentAmount.HasValue)
        {
            var receipt = await _numbering.NextReceipt(DateTime.Now.Year);
            var final = req.StageId == ProjectCompletion;

            _db.Payments.Add(new Payment
            {
                ProjectId = project.ProjectId,
                Amount = req.PaymentAmount.Value,
                PaymentType = final ? "final" : "design_approval",
                PaymentDate = when,
                Note = final ? "Final payment" : "Fee to continue after design approval",
                ReceiptNumber = receipt
            });
            _db.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = project.ProjectId,
                EventType = "payment_received",
                Description = $"Payment of {req.PaymentAmount.Value:N2} received ({(final ? "final payment" : "design approval fee")}, receipt {receipt})."
            });
        }

        if (req.StageId == ProjectCompletion && req.CompleteProject == true)
        {
            project.Status = "completed";
            track.Status = "completed";
            track.CompletedAt = DateTime.UtcNow;
            _db.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = project.ProjectId,
                EventType = "project_status_change",
                Description = "Project marked completed."
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/projecttracks/5/status   body: { "status": "blocked" }
    [HttpPut("{id:int}/status")]
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