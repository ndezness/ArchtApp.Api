using ArchtApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArchtApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class CalendarController : ControllerBase
{
    private readonly CalendarSync _calendar;

    public CalendarController(CalendarSync calendar) => _calendar = calendar;

    // GET api/calendar/status -> is the Google Calendar connection working?
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var s = await _calendar.Status();
        return Ok(new { s.Configured, s.Ok, s.Message });
    }
}