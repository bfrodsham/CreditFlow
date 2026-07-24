namespace CreditFlow.Contracts;

public sealed record PaymentWebhookResultDto(
    Guid AccountId,
    int CreditsGranted,
    string IdempotencyKey,
    string ReferenceId,
    int Balance,
    bool AlreadyProcessed);
