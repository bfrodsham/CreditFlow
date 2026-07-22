namespace CreditFlow.Domain;

public class CreditLedgerEntry
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public int Amount { get; set; }
    public CreditLedgerEntryType Type { get; set; }
    public CreditLedgerEntrySource Source { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string ReferenceId { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
