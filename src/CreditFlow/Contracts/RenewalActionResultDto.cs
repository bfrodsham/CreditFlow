namespace CreditFlow.Contracts;

public sealed record RenewalActionResultDto(
    int AccountsQueued,
    int RenewalsProcessed,
    DateTimeOffset RequestedAtUtc);