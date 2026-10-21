using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StagesController : ControllerBase
{
    private readonly ArchtAppDbContext _db;

    public StagesController(ArchtAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Stage>>> GetAll() =>
        await _db.Stages.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<Stage>> Create(Stage stage)
    {
        _db.Stages.Add(stage);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = stage.StageId }, stage);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Stage stage)
    {
        if (id != stage.StageId) return BadRequest();

        _db.Entry(stage).State = EntityState.Modified;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Stages.AnyAsync(s => s.StageId == id)) return NotFound();
            throw;
        }
        return NoContent();
    }
}