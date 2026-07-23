namespace CreditFlow.Application;

public class InsufficientCreditsException : Exception
{
    public InsufficientCreditsException(Guid accountId, int availableCredits, int requiredCredits)
        : base($"Account '{accountId}' has insufficient credits. Available: {availableCredits}, required: {requiredCredits}.")
    {
        AccountId = accountId;
        AvailableCredits = availableCredits;
        RequiredCredits = requiredCredits;
    }

    public Guid AccountId { get; }
    public int AvailableCredits { get; }
    public int RequiredCredits { get; }
}