namespace CreditFlow.Contracts;

public sealed record UsageEventResultDto(
    Guid UsageEventId,
    Guid AccountId,
    string EventType,
    int CreditCost,
    string IdempotencyKey,
    int Balance);