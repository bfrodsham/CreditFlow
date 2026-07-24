namespace CreditFlow.Application;

public sealed class MalformedWebhookPayloadException : Exception
{
    public MalformedWebhookPayloadException(string message)
        : base(message)
    {
    }
}
