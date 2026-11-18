using ArchtApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Services;

public class NumberingService
{
    private readonly ArchtAppDbContext _db;

    public NumberingService(ArchtAppDbContext db) => _db = db;

    // e.g. BDL-2026-001 (prefix comes from Settings)
    public async Task<string> NextReference(int year)
    {
        var prefix = (await _db.Settings.FindAsync("ReferencePrefix"))?.Value;
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "BDL";
        var stem = $"{prefix}-{year}-";

        var used = await _db.Projects
            .Where(p => p.Reference != null && p.Reference.StartsWith(stem))
            .Select(p => p.Reference!)
            .ToListAsync();

        return $"{stem}{NextNumber(used, stem):000}";
    }

    // e.g. RC-2026-0001
    public async Task<string> NextReceipt(int year)
    {
        var stem = $"RC-{year}-";

        var used = await _db.Payments
            .Where(p => p.ReceiptNumber != null && p.ReceiptNumber.StartsWith(stem))
            .Select(p => p.ReceiptNumber!)
            .ToListAsync();

        return $"{stem}{NextNumber(used, stem):0000}";
    }

    private static int NextNumber(IEnumerable<string> used, string stem) =>
        used.Select(v => int.TryParse(v[stem.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
}