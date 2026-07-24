namespace CreditFlow.Contracts;

public sealed record PaymentWebhookPayloadDto(
    Guid AccountId,
    int Credits,
    string IdempotencyKey,
    string ReferenceId,
    string? Description);
