namespace CreditFlow.Domain;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid PlanId { get; set; }
    public DateTimeOffset CurrentPeriodStart { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}