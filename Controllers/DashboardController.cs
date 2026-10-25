using ArchtApp.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private const int StaleAfterDays = 14;
    private readonly ArchtAppDbContext _db;

    public DashboardController(ArchtAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var projects = await _db.Projects.AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.Tracks).ThenInclude(t => t.CurrentStage)
            .Include(p => p.Payments)
            .OrderBy(p => p.ProjectId)
            .ToListAsync();

        // Pull just the timestamps and group in memory (keeps SQLite happy)
        var logTimes = await _db.ActivityLogs.AsNoTracking()
            .Select(l => new { l.ProjectId, l.CreatedAt })
            .ToListAsync();

        var lastActivity = logTimes
            .GroupBy(l => l.ProjectId)
            .ToDictionary(g => g.Key, g => g.Max(l => l.CreatedAt));

        var cutoff = DateTime.UtcNow.AddDays(-StaleAfterDays);

        var rows = projects.Select(p =>
        {
            var paid = p.Payments.Sum(x => x.Amount);
            var quoted = p.TotalQuotedCost ?? 0m;
            DateTime? last = lastActivity.TryGetValue(p.ProjectId, out var d) ? d : null;

            return new
            {
                projectId = p.ProjectId,
                projectName = p.ProjectName,
                status = p.Status,
                clientName = p.Client?.Name,
                targetCompletionDate = p.TargetCompletionDate,
                tracks = p.Tracks.Select(t => new
                {
                    id = t.ProjectTrackId,
                    name = t.TrackName,
                    status = t.Status,
                    stage = t.CurrentStage?.Name
                }).ToList(),
                totalQuoted = quoted,
                totalPaid = paid,
                balanceDue = quoted - paid,
                lastActivity = last,
                isStale = p.Status == "active" && (last is null || last < cutoff)
            };
        }).ToList();

        var open = rows.Where(r => r.status == "active").ToList();

        return Ok(new
        {
            summary = new
            {
                activeProjects = open.Count,
                blockedTracks = open.Sum(r => r.tracks.Count(t => t.status == "blocked")),
                outstandingBalance = open.Sum(r => r.balanceDue),
                staleProjects = open.Count(r => r.isStale),
                staleAfterDays = StaleAfterDays
            },
            projects = rows
        });
    }
}