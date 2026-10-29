using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using ArchtApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record SetupRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ArchtAppDbContext _db;
    private readonly TokenService _tokens;

    public AuthController(ArchtAppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    private int CurrentUserId() =>
        int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    private object SignedIn(User user)
    {
        var (token, expiresAt) = _tokens.Create(user);
        return new { token, expiresAt, username = user.Username, role = user.Role };
    }

    // Lets the login page know whether to show "create the admin account" instead
    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<IActionResult> Status() =>
        Ok(new { needsSetup = !await _db.Users.AnyAsync() });

    // Works exactly once: only while the Users table is empty
    [AllowAnonymous]
    [HttpPost("setup")]
    public async Task<IActionResult> Setup(SetupRequest req)
    {
        if (await _db.Users.AnyAsync())
            return Conflict("Setup has already been completed.");

        var username = UserRules.Normalize(req.Username);
        var error = UserRules.ValidateUsername(username) ?? UserRules.ValidatePassword(req.Password);
        if (error is not null) return BadRequest(error);

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "admin"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(SignedIn(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var username = UserRules.Normalize(req.Username);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        // Same message for "no such user" and "wrong password"
        if (user is null || string.IsNullOrEmpty(req.Password) ||
            !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid username or password.");

        return Ok(SignedIn(user));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() =>
        Ok(new { username = User.FindFirst("name")?.Value, role = User.FindFirst("role")?.Value });

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var user = await _db.Users.FindAsync(CurrentUserId());
        if (user is null) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword ?? "", user.PasswordHash))
            return BadRequest("Current password is incorrect.");

        var error = UserRules.ValidatePassword(req.NewPassword);
        if (error is not null) return BadRequest(error);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}