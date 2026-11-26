using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using ArchtApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record LeadRequest(
    string Name, string? Phone, string? Email, string SiteAddress, string? ProjectInterest, string? Notes);
public record LeadStatusRequest(string Status);
public record ConvertLeadRequest(
    DateTime ConsultationStart, decimal Fee, DateTime? PaidAt, string? ProjectName, int? ExistingClientId);

[ApiController]
[Route("api/[controller]")]
public class LeadsController : ControllerBase
{
    private readonly ArchtAppDbContext _db;
    private readonly SchedulingService _scheduling;
    private readonly NumberingService _numbering;
    private readonly CalendarSync _calendar;

    public LeadsController(
        ArchtAppDbContext db, SchedulingService scheduling, NumberingService numbering, CalendarSync calendar)
    {
        _db = db;
        _scheduling = scheduling;
        _numbering = numbering;
        _calendar = calendar;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // GET api/leads?status=open|converted|lost|all (default open)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        status = string.IsNullOrWhiteSpace(status) ? "open" : status.ToLowerInvariant();

        var query = _db.Leads.AsNoTracking().AsQueryable();
        if (status != "all") query = query.Where(l => l.Status == status);

        return Ok(await query.OrderByDescending(l => l.CreatedAt).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Lead>> Get(int id)
    {
        var lead = await _db.Leads.FindAsync(id);
        return lead is null ? NotFound() : lead;
    }

    [HttpPost]
    public async Task<ActionResult<Lead>> Create(LeadRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.SiteAddress))
            return BadRequest("Name and site address are required.");

        var lead = new Lead
        {
            Name = req.Name.Trim(),
            Phone = Clean(req.Phone),
            Email = Clean(req.Email),
            SiteAddress = req.SiteAddress.Trim(),
            ProjectInterest = Clean(req.ProjectInterest),
            Notes = Clean(req.Notes)
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = lead.LeadId }, lead);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LeadRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.SiteAddress))
            return BadRequest("Name and site address are required.");

        var lead = await _db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        if (lead.Status == "converted")
            return BadRequest("This lead has been converted. Edit the client or project instead.");

        lead.Name = req.Name.Trim();
        lead.Phone = Clean(req.Phone);
        lead.Email = Clean(req.Email);
        lead.SiteAddress = req.SiteAddress.Trim();
        lead.ProjectInterest = Clean(req.ProjectInterest);
        lead.Notes = Clean(req.Notes);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/leads/5/status   body: { "status": "lost" } or { "status": "open" }
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, LeadStatusRequest req)
    {
        var status = req.Status?.Trim().ToLowerInvariant();
        if (status != "open" && status != "lost")
            return BadRequest("Status must be open or lost. Use convert to make a client.");

        var lead = await _db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        if (lead.Status == "converted") return BadRequest("This lead has already been converted.");

        lead.Status = status;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        if (lead.Status == "converted")
            return Conflict("A converted lead is part of the project history and can't be deleted.");

        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Creates the client (or reuses a returning one), the project with its reference number,
    // the Main track, the consultation appointment and the consultation fee payment.
    [HttpPost("{id:int}/convert")]
    public async Task<IActionResult> ConvertToClient(int id, ConvertLeadRequest req)
    {
        var lead = await _db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        if (lead.Status == "converted") return Conflict("This lead has already been converted.");
        if (lead.Status == "lost") return BadRequest("Reopen this lead before converting it.");

        if (req.Fee <= 0)
            return BadRequest("The consultation fee must be paid before a client can be created.");
        if (req.ConsultationStart == default)
            return BadRequest("Choose the consultation date and time.");

        var slotProblem = await _scheduling.CheckSlot(req.ConsultationStart, SchedulingService.DefaultMinutes);
        if (slotProblem is not null) return BadRequest(slotProblem);

        var firstStage = await _db.Stages.OrderBy(s => s.SortOrder).FirstOrDefaultAsync();
        if (firstStage is null) return BadRequest("No project stages are configured.");

        Client client;
        var returning = req.ExistingClientId.HasValue;
        if (returning)
        {
            var existing = await _db.Clients.FindAsync(req.ExistingClientId!.Value);
            if (existing is null) return BadRequest($"Client {req.ExistingClientId} does not exist.");
            client = existing;
        }
        else
        {
            client = new Client
            {
                Name = lead.Name,
                Email = lead.Email,
                Phone = lead.Phone,
                PropertyAddress = lead.SiteAddress
            };
        }

        // Times are kept as entered (Jamaica time), not converted
        var now = DateTime.Now;
        var paidAt = req.PaidAt ?? now;
        var interest = Clean(lead.ProjectInterest) ?? "New project";
        var projectName = Clean(req.ProjectName) ?? $"{interest} - {lead.Name}";

        await using var tx = await _db.Database.BeginTransactionAsync();

        var reference = await _numbering.NextReference(now.Year);
        var receipt = await _numbering.NextReceipt(now.Year);

        var project = new Project
        {
            Client = client,
            ProjectName = projectName,
            Status = "active",
            StartDate = req.ConsultationStart.Date,
            Reference = reference,
            SiteAddress = lead.SiteAddress,
            ProjectInterest = lead.ProjectInterest
        };

        project.Tracks.Add(new ProjectTrack { TrackName = "Main", CurrentStageId = firstStage.StageId });

        project.Payments.Add(new Payment
        {
            Amount = req.Fee,
            PaymentType = "consultation",
            PaymentDate = paidAt,
            Note = "Consultation fee",
            ReceiptNumber = receipt
        });

        project.ActivityLogs.Add(new ActivityLog
        {
            EventType = "lead_converted",
            Description = returning
                ? $"Lead converted to a new project for existing client {client.Name}. Reference {reference}."
                : $"Lead converted to client {client.Name}. Reference {reference}."
        });
        project.ActivityLogs.Add(new ActivityLog
        {
            EventType = "payment_received",
            Description = $"Consultation fee of {req.Fee:N2} received (receipt {receipt})."
        });
        project.ActivityLogs.Add(new ActivityLog
        {
            EventType = "consultation_scheduled",
            Description = $"Consultation scheduled for {req.ConsultationStart:yyyy-MM-dd HH:mm}."
        });

        _db.Projects.Add(project);
        _db.WorkItems.Add(new WorkItem
        {
            Project = project,
            Type = "consultation",
            Title = $"Consultation - {client.Name}",
            DueDate = req.ConsultationStart,
            DurationMinutes = SchedulingService.DefaultMinutes
        });

        await _db.SaveChangesAsync();

        lead.Status = "converted";
        lead.ConvertedProjectId = project.ProjectId;
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        var consultationId = await _db.WorkItems
            .Where(w => w.ProjectId == project.ProjectId && w.Type == "consultation")
            .Select(w => w.WorkItemId)
            .FirstAsync();
        await _calendar.SyncAppointment(consultationId);
        return Ok(new
        {
            projectId = project.ProjectId,
            clientId = client.ClientId,
            reference,
            receiptNumber = receipt
        });
    }
}