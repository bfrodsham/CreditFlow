namespace CreditFlow.Domain;

public enum CreditLedgerEntrySource
{
    SubscriptionRenewal = 1,
    CreditPackPurchase = 2,
    UsageEvent = 3,
    ManualAdjustment = 4
}
