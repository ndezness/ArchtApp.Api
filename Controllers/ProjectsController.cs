using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ArchtAppDbContext _db;

    public ProjectsController(ArchtAppDbContext db) => _db = db;

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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Project project)
    {
        if (id != project.ProjectId) return BadRequest();

        _db.Entry(project).State = EntityState.Modified;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Projects.AnyAsync(p => p.ProjectId == id)) return NotFound();
            throw;
        }
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