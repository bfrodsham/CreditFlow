namespace CreditFlow.Domain;

public class UsageEvent
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public required string EventType { get; set; }
    public int CreditCost { get; set; }
    public required string IdempotencyKey { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}