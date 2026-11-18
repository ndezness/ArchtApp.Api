using ArchtApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Services;

public class SchedulingService
{
    public const int DefaultMinutes = 60;

    private readonly ArchtAppDbContext _db;

    public SchedulingService(ArchtAppDbContext db) => _db = db;

    // Returns a message describing the problem, or null if the slot is free
    public async Task<string?> CheckSlot(DateTime start, int minutes, int? ignoreWorkItemId = null)
    {
        if (start.DayOfWeek == DayOfWeek.Sunday)
            return "No appointments are booked on Sundays.";

        var end = start.AddMinutes(minutes);
        var dayStart = start.Date;
        var dayEnd = dayStart.AddDays(1);

        var sameDay = await _db.WorkItems.AsNoTracking()
            .Where(w => (w.Type == "consultation" || w.Type == "site_visit")
                        && !w.IsDone
                        && w.DueDate != null
                        && w.DueDate >= dayStart && w.DueDate < dayEnd
                        && (ignoreWorkItemId == null || w.WorkItemId != ignoreWorkItemId))
            .ToListAsync();

        foreach (var w in sameDay)
        {
            var wStart = w.DueDate!.Value;
            var wEnd = wStart.AddMinutes(w.DurationMinutes ?? DefaultMinutes);

            // Back-to-back appointments are fine; only real overlaps are refused
            if (start < wEnd && end > wStart)
                return $"That time overlaps '{w.Title}' ({wStart:HH:mm}-{wEnd:HH:mm}).";
        }

        return null;
    }
}