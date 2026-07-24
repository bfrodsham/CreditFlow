namespace CreditFlow.Contracts;

public sealed record SubscriptionDto(
    Guid AccountId,
    Guid PlanId,
    string PlanName,
    int MonthlyCreditAllowance,
    bool AllowsRollover,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    string Status);