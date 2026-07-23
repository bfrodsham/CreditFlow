namespace CreditFlow.Application;

public sealed class InvalidWebhookSignatureException : Exception
{
    public InvalidWebhookSignatureException(string message)
        : base(message)
    {
    }
}
