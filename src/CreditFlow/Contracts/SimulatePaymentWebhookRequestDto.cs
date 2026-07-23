namespace CreditFlow.Contracts;

public sealed record SimulatePaymentWebhookRequestDto(
    Guid AccountId,
    int? Credits,
    string? IdempotencyKey,
    string? Description);
