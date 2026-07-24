namespace CreditFlow.Domain;

public class Plan
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int MonthlyCreditAllowance { get; set; }
    public bool AllowsRollover { get; set; }
    public decimal PricePerExtraCredit { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}