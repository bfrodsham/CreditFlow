namespace CreditFlow.Contracts;

public sealed record SimulateUsageEventRequestDto(Guid AccountId, string EventType, string? IdempotencyKey);