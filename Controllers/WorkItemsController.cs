using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreateWorkItemRequest(
    int ProjectId, string Type, string Title, DateTime? DueDate, string? Notes);
public record SetDoneRequest(bool IsDone);

[ApiController]
[Route("api/[controller]")]
public class WorkItemsController : ControllerBase
{
    private static readonly string[] ValidTypes = { "task", "site_visit" };
    private readonly ArchtAppDbContext _db;

    public WorkItemsController(ArchtAppDbContext db) => _db = db;

    // GET api/workitems?type=task&status=open&projectId=1
    // status: open (default), done, or all
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type, [FromQuery] string? status, [FromQuery] int? projectId)
    {
        var query = _db.WorkItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(w => w.Type == type);
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
                clientName = w.Project.Client!.Name,
                w.Type,
                w.Title,
                w.Notes,
                w.DueDate,
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

    [HttpPost]
    public async Task<ActionResult<WorkItem>> Create(CreateWorkItemRequest req)
    {
        var type = req.Type?.Trim().ToLowerInvariant();
        if (type is null || !ValidTypes.Contains(type))
            return BadRequest("Type must be one of: task, site_visit.");

        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("A title is required.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
            return BadRequest($"Project {req.ProjectId} does not exist.");

        var item = new WorkItem
        {
            ProjectId = req.ProjectId,
            Type = type,
            Title = req.Title.Trim(),
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            DueDate = req.DueDate
        };

        _db.WorkItems.Add(item);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = item.ProjectId,
            EventType = type == "site_visit" ? "site_visit_scheduled" : "task_created",
            Description = type == "site_visit"
                ? $"Site visit scheduled: '{item.Title}'" +
                  (item.DueDate.HasValue ? $" for {item.DueDate:yyyy-MM-dd}." : ".")
                : $"Task added: '{item.Title}'."
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.WorkItemId }, item);
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

        var label = item.Type == "site_visit" ? "Site visit" : "Task";
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

        _db.WorkItems.Remove(item);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = item.ProjectId,
            EventType = "work_item_removed",
            Description = $"'{item.Title}' was removed."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }
}