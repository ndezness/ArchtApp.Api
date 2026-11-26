using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using ArchtApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreateWorkItemRequest(
    int ProjectId, string Type, string? Title, DateTime? DueDate,
    int? DurationMinutes, string? Address, string? Notes);
public record UpdateAppointmentRequest(
    DateTime DueDate, int? DurationMinutes, string? Address, string? Notes);
public record SetDoneRequest(bool IsDone);
public record NotesRequest(string? Notes);

[ApiController]
[Route("api/[controller]")]
public class WorkItemsController : ControllerBase
{
    // Consultations are created by converting a lead, never directly
    private static readonly string[] ValidTypes = { "task", "site_visit" };

    private readonly ArchtAppDbContext _db;
    private readonly SchedulingService _scheduling;
    private readonly CalendarSync _calendar;                       // calendar edit 1 of 5

    public WorkItemsController(ArchtAppDbContext db, SchedulingService scheduling, CalendarSync calendar)
    {                                                              // calendar edit 1 of 5
        _db = db;
        _scheduling = scheduling;
        _calendar = calendar;                                      // calendar edit 1 of 5
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool ValidDuration(int minutes) => minutes is >= 15 and <= 480;

    // GET api/workitems?type=consultation,site_visit&status=open&projectId=1
    // type can be one value or a comma-separated list; status: open (default), done or all
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type, [FromQuery] string? status, [FromQuery] int? projectId)
    {
        var query = _db.WorkItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var types = type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            query = query.Where(w => types.Contains(w.Type));
        }
        if (projectId.HasValue) query = query.Where(w => w.ProjectId == projectId.Value);

        status = string.IsNullOrWhiteSpace(status) ? "open" : status.ToLowerInvariant();
        if (status == "open") query = query.Where(w => !w.IsDone);
        else if (status == "done") query = query.Where(w => w.IsDone);

        var items = await query
            .OrderBy(w => w.IsDone)
            .ThenBy(w => w.DueDate == null)
            .ThenBy(w => w.DueDate)
            .Select(w => new
            {
                w.WorkItemId,
                w.ProjectId,
                projectName = w.Project!.ProjectName,
                reference = w.Project.Reference,
                clientName = w.Project.Client!.Name,
                w.Type,
                w.Title,
                w.Notes,
                w.DueDate,
                w.DurationMinutes,
                w.Address,
                w.VisitNumber,
                onCalendar = w.GoogleEventId != null,              // calendar edit 2 of 5
                w.IsDone,
                w.CompletedAt,
                w.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkItem>> Get(int id)
    {
        var item = await _db.WorkItems.FindAsync(id);
        return item is null ? NotFound() : item;
    }

    // GET api/workitems/5/detail -> one item with its project and client names
    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> Detail(int id)
    {
        var item = await _db.WorkItems.AsNoTracking()
            .Where(w => w.WorkItemId == id)
            .Select(w => new
            {
                w.WorkItemId,
                w.ProjectId,
                projectName = w.Project!.ProjectName,
                reference = w.Project.Reference,
                clientName = w.Project.Client!.Name,
                w.Type,
                w.Title,
                w.Notes,
                w.DueDate,
                w.DurationMinutes,
                w.Address,
                w.VisitNumber,
                w.IsDone,
                w.CompletedAt,
                w.CreatedAt
            })
            .FirstOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    // PUT api/workitems/5/notes   body: { "notes": "..." }
    // Works even after the visit is marked done, because notes are often written afterwards
    [HttpPut("{id:int}/notes")]
    public async Task<IActionResult> SetNotes(int id, NotesRequest req)
    {
        var item = await _db.WorkItems.FindAsync(id);
        if (item is null) return NotFound();

        var notes = Clean(req.Notes);
        if (notes is { Length: > 5000 }) return BadRequest("Notes can be at most 5000 characters.");
        if (notes == item.Notes) return NoContent();

        item.Notes = notes;

        var label = item.Type == "site_visit" ? $"Site visit {item.VisitNumber}" : item.Title;
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = item.ProjectId,
            EventType = "site_notes_updated",
            Description = $"Notes updated for {label}."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<WorkItem>> Create(CreateWorkItemRequest req)
    {
        var type = req.Type?.Trim().ToLowerInvariant();
        if (type is null || !ValidTypes.Contains(type))
            return BadRequest("Type must be task or site_visit. Consultations are created when a lead is converted.");

        var project = await _db.Projects.Include(p => p.Client)
            .FirstOrDefaultAsync(p => p.ProjectId == req.ProjectId);
        if (project is null) return BadRequest($"Project {req.ProjectId} does not exist.");

        var item = new WorkItem
        {
            ProjectId = project.ProjectId,
            Type = type,
            Notes = Clean(req.Notes),
            DueDate = req.DueDate
        };

        if (type == "task")
        {
            if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("A title is required.");
            item.Title = req.Title.Trim();
        }
        else
        {
            if (req.DueDate is null) return BadRequest("Choose the visit date and time.");

            var minutes = req.DurationMinutes ?? SchedulingService.DefaultMinutes;
            if (!ValidDuration(minutes))
                return BadRequest("The duration must be between 15 minutes and 8 hours.");

            var problem = await _scheduling.CheckSlot(req.DueDate.Value, minutes);
            if (problem is not null) return BadRequest(problem);

            // Visits are numbered per project: Visit 1, Visit 2, ...
            var last = await _db.WorkItems
                .Where(w => w.ProjectId == project.ProjectId && w.Type == "site_visit")
                .Select(w => (int?)w.VisitNumber)
                .MaxAsync();

            item.VisitNumber = (last ?? 0) + 1;
            item.DurationMinutes = minutes;
            item.Address = Clean(req.Address) ?? project.SiteAddress ?? project.Client?.PropertyAddress;
            item.Title = Clean(req.Title) ?? $"Site visit {item.VisitNumber} - {project.Client?.Name}";
        }

        _db.WorkItems.Add(item);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = item.ProjectId,
            EventType = type == "site_visit" ? "site_visit_scheduled" : "task_created",
            Description = type == "site_visit"
                ? $"Site visit {item.VisitNumber} scheduled for {item.DueDate:yyyy-MM-dd HH:mm} at {item.Address}."
                : $"Task added: '{item.Title}'."
        });

        await _db.SaveChangesAsync();
        await _calendar.SyncAppointment(item.WorkItemId);          // calendar edit 3 of 5
        return CreatedAtAction(nameof(Get), new { id = item.WorkItemId }, item);
    }

    // PUT api/workitems/5 -> reschedule or edit an appointment
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAppointment(int id, UpdateAppointmentRequest req)
    {
        var item = await _db.WorkItems.FindAsync(id);
        if (item is null) return NotFound();
        if (item.Type != "site_visit" && item.Type != "consultation")
            return BadRequest("Only appointments can be rescheduled.");
        if (item.IsDone) return BadRequest("Reopen this appointment before changing it.");

        var minutes = req.DurationMinutes ?? item.DurationMinutes ?? SchedulingService.DefaultMinutes;
        if (!ValidDuration(minutes))
            return BadRequest("The duration must be between 15 minutes and 8 hours.");

        var problem = await _scheduling.CheckSlot(req.DueDate, minutes, ignoreWorkItemId: id);
        if (problem is not null) return BadRequest(problem);

        var label = item.Type == "consultation" ? "Consultation" : $"Site visit {item.VisitNumber}";
        var oldStart = item.DueDate;
        var address = Clean(req.Address);

        if (oldStart != req.DueDate)
        {
            _db.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = item.ProjectId,
                EventType = "appointment_rescheduled",
                Description = $"{label} rescheduled from {oldStart:yyyy-MM-dd HH:mm} to {req.DueDate:yyyy-MM-dd HH:mm}."
            });
        }
        if (address != item.Address)
        {
            _db.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = item.ProjectId,
                EventType = "appointment_updated",
                Description = $"{label} address changed to {address ?? "none"}."
            });
        }

        item.DueDate = req.DueDate;
        item.DurationMinutes = minutes;
        item.Address = address;
        item.Notes = Clean(req.Notes);

        await _db.SaveChangesAsync();
        await _calendar.SyncAppointment(item.WorkItemId);          // calendar edit 4 of 5
        return NoContent();
    }

    // PUT api/workitems/5/done   body: { "isDone": true }
    [HttpPut("{id:int}/done")]
    public async Task<IActionResult> SetDone(int id, SetDoneRequest req)
    {
        var item = await _db.WorkItems.FindAsync(id);
        if (item is null) return NotFound();

        if (item.IsDone == req.IsDone) return NoContent();

        item.IsDone = req.IsDone;
        item.CompletedAt = req.IsDone ? DateTime.UtcNow : null;

        var label = item.Type switch
        {
            "site_visit" => "Site visit",
            "consultation" => "Consultation",
            _ => "Task"
        };
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = item.ProjectId,
            EventType = req.IsDone ? "work_item_completed" : "work_item_reopened",
            Description = req.IsDone
                ? $"{label} completed: '{item.Title}'."
                : $"{label} reopened: '{item.Title}'."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.WorkItems.FindAsync(id);
        if (item is null) return NotFound();

        // Photos belong to a specific visit, so they have to be removed first
        if (await _db.Documents.AnyAsync(d => d.WorkItemId == id))
            return Conflict("This visit has photos or files attached. Remove them first.");

        var eventId = item.GoogleEventId;                          // calendar edit 5 of 5

        _db.WorkItems.Remove(item);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = item.ProjectId,
            EventType = "work_item_removed",
            Description = $"'{item.Title}' was removed."
        });

        await _db.SaveChangesAsync();
        await _calendar.RemoveEvent(eventId);                      // calendar edit 5 of 5
        return NoContent();
    }
}