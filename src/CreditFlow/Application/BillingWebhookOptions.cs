namespace CreditFlow.Application;

public sealed class BillingWebhookOptions
{
    public const string SectionName = "BillingWebhook";

    public string SigningSecret { get; set; } = string.Empty;
}
