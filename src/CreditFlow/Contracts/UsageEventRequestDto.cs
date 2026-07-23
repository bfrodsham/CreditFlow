namespace CreditFlow.Contracts;

public sealed record UsageEventRequestDto(string EventType, string IdempotencyKey);