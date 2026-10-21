using ArchtApp.Api.Data;
using ArchtApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Controllers;

public record CreatePaymentRequest(
    int ProjectId, decimal Amount, string PaymentType, DateTime? PaymentDate, string? Note);

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private static readonly string[] ValidTypes = { "deposit", "milestone", "final" };
    private readonly ArchtAppDbContext _db;

    public PaymentsController(ArchtAppDbContext db) => _db = db;

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
        if (type is null || !ValidTypes.Contains(type))
            return BadRequest("Payment type must be one of: deposit, milestone, final.");

        if (!await _db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
            return BadRequest($"Project {req.ProjectId} does not exist.");

        var payment = new Payment
        {
            ProjectId = req.ProjectId,
            Amount = req.Amount,
            PaymentType = type,
            PaymentDate = req.PaymentDate ?? DateTime.UtcNow,
            Note = req.Note
        };

        _db.Payments.Add(payment);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = req.ProjectId,
            EventType = "payment_received",
            Description = $"Payment of {req.Amount:N2} received ({type})."
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
            Description = $"Payment of {payment.Amount:N2} ({payment.PaymentType}) was removed."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }
}