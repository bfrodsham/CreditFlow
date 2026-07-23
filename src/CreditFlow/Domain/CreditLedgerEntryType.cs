namespace CreditFlow.Domain;

public enum CreditLedgerEntryType
{
    Grant = 1,
    Consume = 2,
    Expire = 3,
    Rollover = 4,
    Adjustment = 5
}
