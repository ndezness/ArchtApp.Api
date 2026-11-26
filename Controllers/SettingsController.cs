using System.Globalization;
using System.Text.RegularExpressions;
using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private static readonly string[] Keys =
    {
        "BusinessName", "BusinessPhone", "BusinessEmail", "BusinessAddress",
        "ReferencePrefix", "ConsultationFee", "GoogleCalendarId"
    };

    private readonly ArchtAppDbContext _db;

    public SettingsController(ArchtAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value));

    [HttpPut]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(Dictionary<string, string> values)
    {
        foreach (var key in values.Keys)
            if (!Keys.Contains(key)) return BadRequest($"Unknown setting '{key}'.");

        if (values.TryGetValue("ConsultationFee", out var fee) &&
            (!decimal.TryParse(fee, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0))
            return BadRequest("The consultation fee must be a number, 0 or more.");

        if (values.TryGetValue("ReferencePrefix", out var prefix) &&
            !Regex.IsMatch(prefix.Trim(), "^[A-Za-z0-9]{2,10}$"))
            return BadRequest("The reference prefix must be 2-10 letters or numbers.");

        foreach (var (key, value) in values)
        {
            var cleaned = key == "ReferencePrefix" ? value.Trim().ToUpperInvariant() : value.Trim();

            var setting = await _db.Settings.FindAsync(key);
            if (setting is null) _db.Settings.Add(new Setting { Key = key, Value = cleaned });
            else setting.Value = cleaned;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}