using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using ArchtApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreatePaymentRequest(
    int ProjectId, decimal Amount, string PaymentType, DateTime? PaymentDate, string? Note);

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private static readonly Dictionary<string, string> Types = new()
    {
        ["consultation"] = "consultation fee",
        ["design_approval"] = "design approval fee",
        ["milestone"] = "milestone payment",
        ["final"] = "final payment"
    };

    private readonly ArchtAppDbContext _db;
    private readonly NumberingService _numbering;

    public PaymentsController(ArchtAppDbContext db, NumberingService numbering)
    {
        _db = db;
        _numbering = numbering;
    }

    // GET api/payments?projectId=1
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Payment>>> GetAll([FromQuery] int? projectId)
    {
        var query = _db.Payments.AsNoTracking().AsQueryable();
        if (projectId.HasValue) query = query.Where(p => p.ProjectId == projectId.Value);
        return await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Payment>> Get(int id)
    {
        var payment = await _db.Payments.FindAsync(id);
        return payment is null ? NotFound() : payment;
    }

    // Everything the receipt page needs: business details, client, payment and the balance
    // as it stood after this payment
    [HttpGet("{id:int}/receipt")]
    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentId == id);
        if (payment is null) return NotFound();

        var project = await _db.Projects.AsNoTracking()
            .Include(p => p.Client)
            .FirstAsync(p => p.ProjectId == payment.ProjectId);

        var all = await _db.Payments.AsNoTracking()
            .Where(p => p.ProjectId == payment.ProjectId)
            .Select(p => new { p.PaymentId, p.Amount, p.PaymentDate })
            .ToListAsync();

        // Payments up to and including this one
        var paidToDate = all
            .Where(p => p.PaymentDate < payment.PaymentDate ||
                        (p.PaymentDate == payment.PaymentDate && p.PaymentId <= payment.PaymentId))
            .Sum(p => p.Amount);

        var quoted = project.TotalQuotedCost;
        decimal? outstanding = quoted is > 0 ? Math.Max(0m, quoted.Value - paidToDate) : null;

        var settings = await _db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
        string S(string key) => settings.TryGetValue(key, out var v) ? v : "";

        return Ok(new
        {
            business = new
            {
                name = S("BusinessName"),
                phone = S("BusinessPhone"),
                email = S("BusinessEmail"),
                address = S("BusinessAddress")
            },
            receiptNumber = payment.ReceiptNumber,
            paymentId = payment.PaymentId,
            paymentType = payment.PaymentType,
            amount = payment.Amount,
            paymentDate = payment.PaymentDate,
            note = payment.Note,
            projectId = project.ProjectId,
            reference = project.Reference,
            projectName = project.ProjectName,
            siteAddress = project.SiteAddress ?? project.Client?.PropertyAddress,
            client = new { name = project.Client?.Name, email = project.Client?.Email, phone = project.Client?.Phone },
            totalQuoted = quoted,
            totalPaid = paidToDate,
            outstanding
        });
    }

    // GET api/payments/summary/1 -> quoted, paid, balance due
    [HttpGet("summary/{projectId:int}")]
    public async Task<IActionResult> Summary(int projectId)
    {
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId);
        if (project is null) return NotFound();

        // SQLite can't sum decimals in SQL, so load the amounts and add them up here
        var amounts = await _db.Payments
            .Where(p => p.ProjectId == projectId)
            .Select(p => p.Amount)
            .ToListAsync();

        var paid = amounts.Sum();
        var quoted = project.TotalQuotedCost ?? 0m;

        return Ok(new
        {
            projectId,
            totalQuotedCost = quoted,
            totalPaid = paid,
            balanceDue = quoted - paid,
            paymentCount = amounts.Count
        });
    }

    [HttpPost]
    public async Task<ActionResult<Payment>> Create(CreatePaymentRequest req)
    {
        if (req.Amount <= 0) return BadRequest("Amount must be greater than zero.");

        var type = req.PaymentType?.Trim().ToLowerInvariant();
        if (type is null || !Types.ContainsKey(type))
            return BadRequest("Payment type must be one of: consultation, design_approval, milestone, final.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
            return BadRequest($"Project {req.ProjectId} does not exist.");

        var receipt = await _numbering.NextReceipt(DateTime.Now.Year);

        var payment = new Payment
        {
            ProjectId = req.ProjectId,
            Amount = req.Amount,
            PaymentType = type,
            PaymentDate = req.PaymentDate ?? DateTime.Now,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            ReceiptNumber = receipt
        };

        _db.Payments.Add(payment);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = req.ProjectId,
            EventType = "payment_received",
            Description = $"Payment of {req.Amount:N2} received ({Types[type]}, receipt {receipt})."
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = payment.PaymentId }, payment);
    }

    // Payments are financial records, so removal is logged rather than silent
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var payment = await _db.Payments.FindAsync(id);
        if (payment is null) return NotFound();

        _db.Payments.Remove(payment);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = payment.ProjectId,
            EventType = "payment_removed",
            Description = $"Payment of {payment.Amount:N2} ({payment.PaymentType}, receipt {payment.ReceiptNumber ?? "none"}) was removed."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }
}