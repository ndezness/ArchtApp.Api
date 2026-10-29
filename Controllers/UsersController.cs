using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using ArchtApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreateUserRequest(string Username, string Password, string Role);
public record ResetPasswordRequest(string NewPassword);
public record ChangeRoleRequest(string Role);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class UsersController : ControllerBase
{
    private readonly ArchtAppDbContext _db;

    public UsersController(ArchtAppDbContext db) => _db = db;

    private int CurrentUserId() =>
        int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Users.AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new { u.UserId, u.Username, u.Role })
            .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req)
    {
        var username = UserRules.Normalize(req.Username);
        var error = UserRules.ValidateUsername(username) ?? UserRules.ValidatePassword(req.Password);
        if (error is not null) return BadRequest(error);

        var role = (req.Role ?? "").Trim().ToLowerInvariant();
        if (!UserRules.Roles.Contains(role))
            return BadRequest("Role must be admin or staff.");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return Conflict($"The username '{username}' is already taken.");

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Created($"/api/users/{user.UserId}", new { user.UserId, user.Username, user.Role });
    }

    // Admin sets a temporary password; the person can change it themselves afterward
    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest req)
    {
        var error = UserRules.ValidatePassword(req.NewPassword);
        if (error is not null) return BadRequest(error);

        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, ChangeRoleRequest req)
    {
        var role = (req.Role ?? "").Trim().ToLowerInvariant();
        if (!UserRules.Roles.Contains(role))
            return BadRequest("Role must be admin or staff.");

        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        // Never allow the last admin to be demoted, or nobody could manage the team
        if (user.Role == "admin" && role != "admin" &&
            await _db.Users.CountAsync(u => u.Role == "admin") <= 1)
            return BadRequest("There must be at least one admin.");

        user.Role = role;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        // Because only admins reach this and you can't remove yourself,
        // at least one admin always remains
        if (id == CurrentUserId())
            return BadRequest("You can't remove your own account.");

        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}