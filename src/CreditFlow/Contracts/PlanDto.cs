namespace CreditFlow.Contracts;

public sealed record PlanDto(
    Guid Id,
    string Name,
    int MonthlyCreditAllowance,
    bool AllowsRollover,
    decimal PricePerExtraCredit);