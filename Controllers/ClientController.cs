using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record ClientRequest(string Name, string? Email, string? Phone, string PropertyAddress);

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly ArchtAppDbContext _db;

    public ClientsController(ArchtAppDbContext db) => _db = db;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Includes project counts so the UI can split current and past clients
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Clients.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.ClientId,
                c.Name,
                c.Email,
                c.Phone,
                c.PropertyAddress,
                c.CreatedAt,
                projectCount = c.Projects.Count(),
                openProjects = c.Projects.Count(p => p.Status == "active" || p.Status == "on_hold")
            })
            .ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Client>> Get(int id)
    {
        var client = await _db.Clients
            .Include(c => c.Projects)
            .FirstOrDefaultAsync(c => c.ClientId == id);

        return client is null ? NotFound() : client;
    }

    [HttpPost]
    public async Task<ActionResult<Client>> Create(ClientRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.PropertyAddress))
            return BadRequest("Name and property address are required.");

        var client = new Client
        {
            Name = req.Name.Trim(),
            Email = Clean(req.Email),
            Phone = Clean(req.Phone),
            PropertyAddress = req.PropertyAddress.Trim()
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = client.ClientId }, client);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ClientRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.PropertyAddress))
            return BadRequest("Name and property address are required.");

        var client = await _db.Clients.FindAsync(id);
        if (client is null) return NotFound();

        client.Name = req.Name.Trim();
        client.Email = Clean(req.Email);
        client.Phone = Clean(req.Phone);
        client.PropertyAddress = req.PropertyAddress.Trim();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Only for clients created by mistake: a client with projects must be closed out, not deleted
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client is null) return NotFound();

        if (await _db.Projects.AnyAsync(p => p.ClientId == id))
            return Conflict("This client has projects, so it can't be deleted. Mark their projects completed or cancelled instead.");

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}