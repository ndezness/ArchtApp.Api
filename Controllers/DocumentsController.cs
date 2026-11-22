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
    private const long MaxUploadBytes = 100L * 1024 * 1024;
    private const string Binary = "application/octet-stream";

    // Only these extensions can be uploaded. Only PDFs and images are served with
    // a real content type (so they can be previewed); everything else is download-only.
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".dwg"] = Binary,
        [".dxf"] = Binary,
        [".skp"] = Binary,
        [".rvt"] = Binary,
        [".ifc"] = Binary,
        [".doc"] = Binary,
        [".docx"] = Binary,
        [".xls"] = Binary,
        [".xlsx"] = Binary,
        [".ppt"] = Binary,
        [".pptx"] = Binary,
        [".txt"] = Binary,
        [".csv"] = Binary,
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    };

    private readonly ArchtAppDbContext _db;
    private readonly string _uploadsDir;

    public DocumentsController(ArchtAppDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        var configured = config["Uploads:Path"];
        _uploadsDir = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "Uploads")
                : configured);
    }

    // Only project-level documents are versioned. Site visit photos are not.
    private async Task<int> NextVersion(int projectId, string name)
    {
        var latest = await _db.Documents
            .Where(d => d.ProjectId == projectId && d.WorkItemId == null
                        && d.FileName.ToLower() == name.ToLower())
            .Select(d => (int?)d.VersionNumber)
            .MaxAsync();
        return (latest ?? 0) + 1;
    }

    // GET api/documents?projectId=1            -> everything for a project
    // GET api/documents?workItemId=5           -> the photos attached to one site visit
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Document>>> GetAll(
        [FromQuery] int? projectId, [FromQuery] int? workItemId)
    {
        var query = _db.Documents.AsNoTracking().AsQueryable();
        if (projectId.HasValue) query = query.Where(d => d.ProjectId == projectId.Value);

        if (workItemId.HasValue)
        {
            return await query
                .Where(d => d.WorkItemId == workItemId.Value)
                .OrderBy(d => d.UploadedAt)
                .ToListAsync();
        }

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

    // GET api/documents/current/1 -> only the latest version of each project document
    [HttpGet("current/{projectId:int}")]
    public async Task<ActionResult<IEnumerable<Document>>> Current(int projectId)
    {
        var docs = await _db.Documents.AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.WorkItemId == null)
            .ToListAsync();

        return docs
            .GroupBy(d => d.FileName.ToLower())
            .Select(g => g.OrderByDescending(d => d.VersionNumber).First())
            .OrderBy(d => d.FileName)
            .ToList();
    }

    // Uploads one file. Without workItemId it is a project document (a file with the same
    // name becomes the next version). With workItemId it is attached to that site visit.
    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<ActionResult<Document>> Upload(
        [FromForm] int projectId, IFormFile file, [FromForm] int? workItemId)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Choose a file to upload.");
        if (file.Length > MaxUploadBytes)
            return BadRequest("Files can be at most 100 MB.");

        var name = Path.GetFileName(file.FileName ?? "").Trim();
        if (name.Length == 0 || name.Length > 200)
            return BadRequest("The file name is missing or too long.");

        var ext = Path.GetExtension(name);
        if (!AllowedTypes.TryGetValue(ext, out var contentType))
            return BadRequest(
                $"'{ext}' files aren't allowed. Allowed types: {string.Join(", ", AllowedTypes.Keys)}.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == projectId))
            return BadRequest($"Project {projectId} does not exist.");

        if (workItemId.HasValue)
        {
            var visit = await _db.WorkItems.AsNoTracking()
                .FirstOrDefaultAsync(w => w.WorkItemId == workItemId.Value);
            if (visit is null || visit.ProjectId != projectId || visit.Type != "site_visit")
                return BadRequest("Photos can only be attached to a site visit of the same project.");
        }

        var version = workItemId.HasValue ? 1 : await NextVersion(projectId, name);

        // The file is stored under a random name, so the original name can never
        // be used to guess a path or overwrite anything
        Directory.CreateDirectory(_uploadsDir);
        var storedName = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
        var fullPath = Path.Combine(_uploadsDir, storedName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var doc = new Document
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            FileName = name,
            VersionNumber = version,
            StoredFileName = storedName,
            ContentType = contentType,
            SizeBytes = file.Length
        };

        _db.Documents.Add(doc);

        // A visit with many photos would flood the timeline, so only project documents are logged
        if (!workItemId.HasValue)
        {
            _db.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = projectId,
                EventType = "document_uploaded",
                Description = $"Document '{name}' uploaded as version {version}."
            });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            System.IO.File.Delete(fullPath); // don't leave an orphan if the record failed
            throw;
        }

        return CreatedAtAction(nameof(Get), new { id = doc.DocumentId }, doc);
    }

    // Records a file name without uploading anything (kept for drawings that only exist on paper)
    [HttpPost]
    public async Task<ActionResult<Document>> Create(CreateDocumentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FileName))
            return BadRequest("File name is required.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
            return BadRequest($"Project {req.ProjectId} does not exist.");

        var name = req.FileName.Trim();

        var doc = new Document
        {
            ProjectId = req.ProjectId,
            FileName = name,
            VersionNumber = await NextVersion(req.ProjectId, name),
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

    // GET api/documents/5/file -> the stored file (login required, like everything else)
    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> GetFile(int id)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == id);
        if (doc is null) return NotFound();
        if (doc.StoredFileName is null) return NotFound("This record has no stored file.");

        var path = Path.Combine(_uploadsDir, doc.StoredFileName);
        if (!System.IO.File.Exists(path)) return NotFound("The file is missing from storage.");

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, doc.ContentType ?? Binary, doc.FileName);
    }

    // Removal of a project document is logged so a missing drawing never disappears without a trace
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc is null) return NotFound();

        var stored = doc.StoredFileName;

        _db.Documents.Remove(doc);
        if (doc.WorkItemId is null)
        {
            _db.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = doc.ProjectId,
                EventType = "document_removed",
                Description = $"Document '{doc.FileName}' version {doc.VersionNumber} was removed."
            });
        }

        await _db.SaveChangesAsync();

        if (stored is not null)
        {
            try { System.IO.File.Delete(Path.Combine(_uploadsDir, stored)); }
            catch (IOException) { /* the record is gone; a leftover file is harmless */ }
        }

        return NoContent();
    }
}