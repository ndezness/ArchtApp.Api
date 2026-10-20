using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly ArchtAppDbContext _db;

    public ClientsController(ArchtAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Client>>> GetAll() =>
        await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Client>> Get(int id)
    {
        var client = await _db.Clients
            .Include(c => c.Projects)
            .FirstOrDefaultAsync(c => c.ClientId == id);

        return client is null ? NotFound() : client;
    }

    [HttpPost]
    public async Task<ActionResult<Client>> Create(Client client)
    {
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = client.ClientId }, client);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Client client)
    {
        if (id != client.ClientId) return BadRequest();

        _db.Entry(client).State = EntityState.Modified;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Clients.AnyAsync(c => c.ClientId == id)) return NotFound();
            throw;
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client is null) return NotFound();

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}