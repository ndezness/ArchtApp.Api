using System.Net;
using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Services;

public class CalendarUnavailableException : Exception
{
    public CalendarUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

public class CalendarSync
{
    private const string TimeZoneId = "America/Jamaica";
    private static readonly TimeSpan Jamaica = TimeSpan.FromHours(-5); // Jamaica has no daylight saving

    private static GoogleCredential? _credential;
    private static string? _credentialPath;

    private readonly ArchtAppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<CalendarSync> _log;

    public CalendarSync(ArchtAppDbContext db, IConfiguration config, ILogger<CalendarSync> log)
    {
        _db = db;
        _config = config;
        _log = log;
    }

    // Appointment times are stored as wall-clock Jamaica time with no time zone attached
    private static DateTimeOffset AtJamaica(DateTime wallClock) =>
        new(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), Jamaica);

    private static bool IsGone(GoogleApiException ex) =>
        ex.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone;

    // Empty means calendar checks are switched off
    private async Task<string?> CalendarId()
    {
        var value = (await _db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "GoogleCalendarId"))?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private CalendarService? Client()
    {
        var path = _config["GoogleCalendar:KeyPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        if (_credential is null || _credentialPath != path)
        {
            _credential = GoogleCredential.FromFile(path).CreateScoped(CalendarService.Scope.Calendar);
            _credentialPath = path;
        }

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential,
            ApplicationName = "ArchtApp"
        });
    }

    // Returns a message describing the clash, or null if the time is free
    public async Task<string?> FindConflict(DateTime start, int minutes, string? ignoreEventId)
    {
        var calendarId = await CalendarId();
        if (calendarId is null) return null; // calendar checks are off

        try
        {
            var svc = Client() ?? throw new InvalidOperationException("The calendar key file isn't set up.");

            var req = svc.Events.List(calendarId);
            req.TimeMinDateTimeOffset = AtJamaica(start);
            req.TimeMaxDateTimeOffset = AtJamaica(start.AddMinutes(minutes));
            req.SingleEvents = true; // expands repeating events
            req.MaxResults = 50;
            var res = await req.ExecuteAsync();

            foreach (var ev in res.Items ?? new List<Event>())
            {
                if (ev.Status == "cancelled" || ev.Id == ignoreEventId) continue;
                if (ev.Transparency == "transparent") continue; // marked "free"

                var name = string.IsNullOrWhiteSpace(ev.Summary) ? "a busy event" : ev.Summary;

                if (ev.Start?.DateTimeDateTimeOffset is { } s && ev.End?.DateTimeDateTimeOffset is { } e)
                {
                    var from = s.ToOffset(Jamaica).DateTime;
                    var to = e.ToOffset(Jamaica).DateTime;
                    return $"That time clashes with '{name}' on the calendar ({from:HH:mm}-{to:HH:mm}).";
                }

                return $"That day has an all-day busy event on the calendar: '{name}'.";
            }

            return null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Google Calendar check failed");
            throw new CalendarUnavailableException("Google Calendar couldn't be checked.", ex);
        }
    }

    private static Event BuildEvent(WorkItem item)
    {
        var start = item.DueDate!.Value;
        var end = start.AddMinutes(item.DurationMinutes ?? SchedulingService.DefaultMinutes);
        var project = item.Project;
        var client = project?.Client;

        var lines = new List<string>();
        if (project is not null) lines.Add($"Project: {project.Reference} {project.ProjectName}".Replace("  ", " ").Trim());
        if (client is not null) lines.Add($"Client: {client.Name}");
        if (!string.IsNullOrWhiteSpace(client?.Phone)) lines.Add($"Phone: {client!.Phone}");
        lines.Add("Booked in ArchtApp");

        return new Event
        {
            Summary = string.IsNullOrWhiteSpace(project?.Reference) ? item.Title : $"{project!.Reference} {item.Title}",
            Location = item.Address ?? project?.SiteAddress,
            Description = string.Join("\n", lines),
            Start = new EventDateTime { DateTimeDateTimeOffset = AtJamaica(start), TimeZone = TimeZoneId },
            End = new EventDateTime { DateTimeDateTimeOffset = AtJamaica(end), TimeZone = TimeZoneId }
        };
    }

    // Creates or updates the calendar event for an appointment. Never throws: the appointment
    // is already saved, so a Google problem is logged and the event can be synced on the next edit.
    public async Task<bool> SyncAppointment(int workItemId)
    {
        try
        {
            var calendarId = await CalendarId();
            var svc = Client();
            if (calendarId is null || svc is null) return false;

            var item = await _db.WorkItems
                .Include(w => w.Project).ThenInclude(p => p!.Client)
                .FirstOrDefaultAsync(w => w.WorkItemId == workItemId);

            if (item is null || item.DueDate is null) return false;
            if (item.Type != "consultation" && item.Type != "site_visit") return false;

            var ev = BuildEvent(item);

            if (item.GoogleEventId is null)
            {
                item.GoogleEventId = (await svc.Events.Insert(ev, calendarId).ExecuteAsync()).Id;
            }
            else
            {
                try
                {
                    await svc.Events.Patch(ev, calendarId, item.GoogleEventId).ExecuteAsync();
                }
                catch (GoogleApiException ex) when (IsGone(ex))
                {
                    // The event was deleted in Google Calendar, so create it again
                    item.GoogleEventId = (await svc.Events.Insert(ev, calendarId).ExecuteAsync()).Id;
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not sync appointment {Id} to Google Calendar", workItemId);
            return false;
        }
    }

    public async Task RemoveEvent(string? eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;

        try
        {
            var calendarId = await CalendarId();
            var svc = Client();
            if (calendarId is null || svc is null) return;

            await svc.Events.Delete(calendarId, eventId).ExecuteAsync();
        }
        catch (GoogleApiException ex) when (IsGone(ex))
        {
            // Already gone from the calendar
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not remove calendar event {EventId}", eventId);
        }
    }

    // Used by the "Test connection" button on the Settings page
    public async Task<(bool Configured, bool Ok, string Message)> Status()
    {
        var calendarId = await CalendarId();
        if (calendarId is null)
            return (false, false, "No Google Calendar ID is set, so appointments are only checked against each other inside ArchtApp.");

        var path = _config["GoogleCalendar:KeyPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return (true, false, "A calendar ID is set, but the key file isn't configured or can't be found. Set GoogleCalendar:KeyPath with dotnet user-secrets, then restart the API.");

        try
        {
            var svc = Client()!;
            var calendar = await svc.Calendars.Get(calendarId).ExecuteAsync();
            return (true, true, $"Connected to '{calendar.Summary}'.");
        }
        catch (GoogleApiException ex)
        {
            var hint = ex.HttpStatusCode switch
            {
                HttpStatusCode.NotFound => "Calendar not found. Check the ID, and that the calendar is shared with the service account.",
                HttpStatusCode.Forbidden => "Access denied. Share the calendar with the service account and choose 'Make changes to events'.",
                _ => ex.Message
            };
            return (true, false, hint);
        }
        catch (Exception ex)
        {
            return (true, false, $"Could not reach Google Calendar: {ex.Message}");
        }
    }
}