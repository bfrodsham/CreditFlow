using System.Data;
using CreditFlow.Domain;
using CreditFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Application;

public class UsageService
{
    private readonly CreditFlowDbContext _dbContext;

    public UsageService(CreditFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UsageEventRecordResult> RecordUsageEvent(Guid accountId, string eventType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type is required.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        var existingResult = await TryGetExistingResultAsync(accountId, idempotencyKey, cancellationToken);
        if (existingResult is not null)
        {
            return existingResult;
        }

        var normalizedEventType = eventType.Trim();
        var creditCost = ResolveCreditCost(normalizedEventType);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var accountExists = await _dbContext.Accounts
            .AnyAsync(account => account.Id == accountId, cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException($"Account '{accountId}' was not found.");
        }

        existingResult = await TryGetExistingResultAsync(accountId, idempotencyKey, cancellationToken);
        if (existingResult is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existingResult;
        }

        var currentBalance = await CreditLedgerService.GetBalanceAsync(_dbContext.CreditLedgerEntries, accountId, cancellationToken);
        if (creditCost > currentBalance)
        {
            throw new InsufficientCreditsException(accountId, currentBalance, creditCost);
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var usageEvent = new UsageEvent
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            EventType = normalizedEventType,
            CreditCost = creditCost,
            IdempotencyKey = idempotencyKey,
            OccurredAt = occurredAt
        };

        var ledgerEntry = new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = -creditCost,
            Type = CreditLedgerEntryType.Consume,
            Source = CreditLedgerEntrySource.UsageEvent,
            IdempotencyKey = idempotencyKey,
            ReferenceId = usageEvent.Id.ToString(),
            Description = $"Usage event: {normalizedEventType}",
            CreatedAt = occurredAt
        };

        _dbContext.UsageEvents.Add(usageEvent);
        _dbContext.CreditLedgerEntries.Add(ledgerEntry);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);

            existingResult = await TryGetExistingResultAsync(accountId, idempotencyKey, cancellationToken);
            if (existingResult is not null)
            {
                return existingResult;
            }

            throw;
        }

        return new UsageEventRecordResult(
            usageEvent.Id,
            usageEvent.AccountId,
            usageEvent.EventType,
            usageEvent.CreditCost,
            usageEvent.IdempotencyKey,
            currentBalance - creditCost);
    }

    private async Task<UsageEventRecordResult?> TryGetExistingResultAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existingLedgerEntry = await _dbContext.CreditLedgerEntries
            .Where(entry => entry.AccountId == accountId && entry.IdempotencyKey == idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingLedgerEntry is null)
        {
            return null;
        }

        if (existingLedgerEntry.Source != CreditLedgerEntrySource.UsageEvent || existingLedgerEntry.Type != CreditLedgerEntryType.Consume)
        {
            throw new ArgumentException(
                $"Idempotency key '{idempotencyKey}' is already in use for this account.",
                nameof(idempotencyKey));
        }

        var existingUsageEvent = await _dbContext.UsageEvents
            .Where(usageEvent => usageEvent.AccountId == accountId && usageEvent.IdempotencyKey == idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);

        var resultingBalance = await CreditLedgerService.GetBalanceAsync(_dbContext.CreditLedgerEntries, accountId, cancellationToken);

        var usageEventId = existingUsageEvent?.Id;
        var parsedUsageEventId = Guid.Empty;
        if (usageEventId is null && !Guid.TryParse(existingLedgerEntry.ReferenceId, out parsedUsageEventId))
        {
            throw new InvalidOperationException(
                $"Usage ledger entry '{existingLedgerEntry.Id}' has an invalid reference id '{existingLedgerEntry.ReferenceId}'.");
        }

        return new UsageEventRecordResult(
            usageEventId ?? parsedUsageEventId,
            accountId,
            existingUsageEvent?.EventType ?? "usage-event",
            existingUsageEvent?.CreditCost ?? Math.Abs(existingLedgerEntry.Amount),
            existingLedgerEntry.IdempotencyKey,
            resultingBalance);
    }

    private static int ResolveCreditCost(string eventType)
    {
        return eventType.ToLowerInvariant() switch
        {
            "api-call" => 10,
            "report-generation" => 15,
            "model-inference" => 25,
            _ => 25
        };
    }
}

public sealed record UsageEventRecordResult(
    Guid UsageEventId,
    Guid AccountId,
    string EventType,
    int CreditCost,
    string IdempotencyKey,
    int Balance);