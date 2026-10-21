using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreateDocumentRequest(int ProjectId, string FileName, string? FilePath);

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly ArchtAppDbContext _db;

    public DocumentsController(ArchtAppDbContext db) => _db = db;

    // GET api/documents?projectId=1 -> every version of every file
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Document>>> GetAll([FromQuery] int? projectId)
    {
        var query = _db.Documents.AsNoTracking().AsQueryable();
        if (projectId.HasValue) query = query.Where(d => d.ProjectId == projectId.Value);
        return await query
            .OrderBy(d => d.FileName)
            .ThenByDescending(d => d.VersionNumber)
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Document>> Get(int id)
    {
        var doc = await _db.Documents.FindAsync(id);
        return doc is null ? NotFound() : doc;
    }

    // GET api/documents/current/1 -> only the latest version of each file
    [HttpGet("current/{projectId:int}")]
    public async Task<ActionResult<IEnumerable<Document>>> Current(int projectId)
    {
        var docs = await _db.Documents.AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .ToListAsync();

        return docs
            .GroupBy(d => d.FileName.ToLower())
            .Select(g => g.OrderByDescending(d => d.VersionNumber).First())
            .OrderBy(d => d.FileName)
            .ToList();
    }

    [HttpPost]
    public async Task<ActionResult<Document>> Create(CreateDocumentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FileName))
            return BadRequest("File name is required.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
            return BadRequest($"Project {req.ProjectId} does not exist.");

        var name = req.FileName.Trim();

        // Next version = highest existing version of this file in this project, plus one
        var latest = await _db.Documents
            .Where(d => d.ProjectId == req.ProjectId && d.FileName.ToLower() == name.ToLower())
            .Select(d => (int?)d.VersionNumber)
            .MaxAsync();

        var doc = new Document
        {
            ProjectId = req.ProjectId,
            FileName = name,
            VersionNumber = (latest ?? 0) + 1,
            FilePath = req.FilePath
        };

        _db.Documents.Add(doc);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = req.ProjectId,
            EventType = "document_uploaded",
            Description = $"Document '{doc.FileName}' recorded as version {doc.VersionNumber}."
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = doc.DocumentId }, doc);
    }

    // Removal is logged so a missing drawing never disappears without a trace
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc is null) return NotFound();

        _db.Documents.Remove(doc);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = doc.ProjectId,
            EventType = "document_removed",
            Description = $"Document '{doc.FileName}' version {doc.VersionNumber} was removed."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }
}