namespace ArchtApp.Api.Models;

public class Payment
{
    public int PaymentId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentType { get; set; } // deposit, milestone, final
    public DateTime PaymentDate { get; set; }
    public string? Note { get; set; }
}