using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CreditFlow.Contracts;
using CreditFlow.Domain;
using CreditFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreditFlow.Application;

public class BillingWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CreditFlowDbContext _dbContext;
    private readonly BillingWebhookOptions _options;

    public BillingWebhookService(CreditFlowDbContext dbContext, IOptions<BillingWebhookOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<PaymentWebhookProcessResult> ProcessPaymentEvent(string payload, string signature, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new MalformedWebhookPayloadException("Webhook payload is required.");
        }

        if (!IsSignatureValid(payload, signature, _options.SigningSecret))
        {
            throw new InvalidWebhookSignatureException("Webhook signature validation failed.");
        }

        var paymentEvent = DeserializePayload(payload);

        var existingResult = await TryGetExistingResultAsync(paymentEvent.AccountId, paymentEvent.IdempotencyKey, cancellationToken);
        if (existingResult is not null)
        {
            return existingResult;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var accountExists = await _dbContext.Accounts
            .AnyAsync(account => account.Id == paymentEvent.AccountId, cancellationToken);
        if (!accountExists)
        {
            throw new InvalidOperationException($"Account '{paymentEvent.AccountId}' was not found.");
        }

        existingResult = await TryGetExistingResultAsync(paymentEvent.AccountId, paymentEvent.IdempotencyKey, cancellationToken);
        if (existingResult is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existingResult;
        }

        var ledgerEntry = new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = paymentEvent.AccountId,
            Amount = paymentEvent.Credits,
            Type = CreditLedgerEntryType.Grant,
            Source = CreditLedgerEntrySource.CreditPackPurchase,
            IdempotencyKey = paymentEvent.IdempotencyKey,
            ReferenceId = paymentEvent.ReferenceId,
            Description = paymentEvent.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.CreditLedgerEntries.Add(ledgerEntry);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);

            existingResult = await TryGetExistingResultAsync(paymentEvent.AccountId, paymentEvent.IdempotencyKey, cancellationToken);
            if (existingResult is not null)
            {
                return existingResult;
            }

            throw;
        }

        var resultingBalance = await CreditLedgerService.GetBalanceAsync(_dbContext.CreditLedgerEntries, paymentEvent.AccountId, cancellationToken);

        return new PaymentWebhookProcessResult(
            paymentEvent.AccountId,
            paymentEvent.Credits,
            paymentEvent.IdempotencyKey,
            paymentEvent.ReferenceId,
            resultingBalance,
            false);
    }

    public static string CreateSignature(string payload, string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload is required.", nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            throw new InvalidOperationException("BillingWebhook signing secret is not configured.");
        }

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var secretBytes = Encoding.UTF8.GetBytes(signingSecret);

        using var hmac = new HMACSHA256(secretBytes);
        var signatureBytes = hmac.ComputeHash(payloadBytes);

        return $"sha256={Convert.ToHexString(signatureBytes).ToLowerInvariant()}";
    }

    private static bool IsSignatureValid(string payload, string signature, string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            throw new InvalidOperationException("BillingWebhook signing secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var normalizedSignature = signature.Trim();
        if (normalizedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            normalizedSignature = normalizedSignature["sha256=".Length..];
        }

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromHexString(normalizedSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = CreateSignature(payload, signingSecret);
        var expectedSignatureHex = expectedSignature["sha256=".Length..];
        var expectedSignatureBytes = Convert.FromHexString(expectedSignatureHex);

        return CryptographicOperations.FixedTimeEquals(expectedSignatureBytes, suppliedSignature);
    }

    private PaymentWebhookPayload DeserializePayload(string payload)
    {
        PaymentWebhookPayloadDto? deserialized;
        try
        {
            deserialized = JsonSerializer.Deserialize<PaymentWebhookPayloadDto>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new MalformedWebhookPayloadException($"Malformed webhook payload: {ex.Message}");
        }

        if (deserialized is null)
        {
            throw new MalformedWebhookPayloadException("Webhook payload is required.");
        }

        if (deserialized.AccountId == Guid.Empty)
        {
            throw new MalformedWebhookPayloadException("Payload accountId must be a non-empty GUID.");
        }

        if (deserialized.Credits <= 0)
        {
            throw new MalformedWebhookPayloadException("Payload credits must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(deserialized.IdempotencyKey))
        {
            throw new MalformedWebhookPayloadException("Payload idempotencyKey is required.");
        }

        if (string.IsNullOrWhiteSpace(deserialized.ReferenceId))
        {
            throw new MalformedWebhookPayloadException("Payload referenceId is required.");
        }

        var description = string.IsNullOrWhiteSpace(deserialized.Description)
            ? "Billing webhook credit grant"
            : deserialized.Description.Trim();

        return new PaymentWebhookPayload(
            deserialized.AccountId,
            deserialized.Credits,
            deserialized.IdempotencyKey.Trim(),
            deserialized.ReferenceId.Trim(),
            description);
    }

    private async Task<PaymentWebhookProcessResult?> TryGetExistingResultAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existingLedgerEntry = await _dbContext.CreditLedgerEntries
            .Where(entry => entry.AccountId == accountId && entry.IdempotencyKey == idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingLedgerEntry is null)
        {
            return null;
        }

        if (existingLedgerEntry.Type != CreditLedgerEntryType.Grant)
        {
            throw new ArgumentException(
                $"Idempotency key '{idempotencyKey}' is already in use for this account.",
                nameof(idempotencyKey));
        }

        var resultingBalance = await CreditLedgerService.GetBalanceAsync(_dbContext.CreditLedgerEntries, accountId, cancellationToken);

        return new PaymentWebhookProcessResult(
            accountId,
            existingLedgerEntry.Amount,
            existingLedgerEntry.IdempotencyKey,
            existingLedgerEntry.ReferenceId,
            resultingBalance,
            true);
    }

    private sealed record PaymentWebhookPayload(
        Guid AccountId,
        int Credits,
        string IdempotencyKey,
        string ReferenceId,
        string Description);
}

public sealed record PaymentWebhookProcessResult(
    Guid AccountId,
    int CreditsGranted,
    string IdempotencyKey,
    string ReferenceId,
    int Balance,
    bool AlreadyProcessed);
