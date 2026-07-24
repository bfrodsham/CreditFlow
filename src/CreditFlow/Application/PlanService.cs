using System.Data;
using CreditFlow.Contracts;
using CreditFlow.Domain;
using CreditFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Application;

public class PlanService
{
	private readonly CreditFlowDbContext _dbContext;

	public PlanService(CreditFlowDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<List<PlanDto>> GetPlansAsync(CancellationToken cancellationToken = default)
	{
		return await _dbContext.Plans
			.OrderBy(plan => plan.MonthlyCreditAllowance)
			.Select(plan => new PlanDto(
				plan.Id,
				plan.Name,
				plan.MonthlyCreditAllowance,
				plan.AllowsRollover,
				plan.PricePerExtraCredit))
			.ToListAsync(cancellationToken);
	}

	public async Task<SubscriptionDto?> GetSubscriptionAsync(Guid accountId, CancellationToken cancellationToken = default)
	{
		var subscription = await _dbContext.Subscriptions
			.Where(item => item.AccountId == accountId)
			.Join(
				_dbContext.Plans,
				subscription => subscription.PlanId,
				plan => plan.Id,
				(subscription, plan) => new SubscriptionDto(
					subscription.AccountId,
					plan.Id,
					plan.Name,
					plan.MonthlyCreditAllowance,
					plan.AllowsRollover,
					subscription.CurrentPeriodStart,
					subscription.CurrentPeriodEnd,
					subscription.Status))
			.SingleOrDefaultAsync(cancellationToken);

		return subscription;
	}

	public async Task<SubscriptionDto> ChangePlanAsync(Guid accountId, Guid planId, CancellationToken cancellationToken = default)
	{
		var subscription = await _dbContext.Subscriptions
			.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken)
			?? throw new InvalidOperationException($"Subscription for account '{accountId}' was not found.");

		var plan = await _dbContext.Plans
			.SingleOrDefaultAsync(item => item.Id == planId, cancellationToken)
			?? throw new InvalidOperationException($"Plan '{planId}' was not found.");

		subscription.PlanId = plan.Id;
		subscription.UpdatedAt = DateTimeOffset.UtcNow;

		await _dbContext.SaveChangesAsync(cancellationToken);

		return new SubscriptionDto(
			subscription.AccountId,
			plan.Id,
			plan.Name,
			plan.MonthlyCreditAllowance,
			plan.AllowsRollover,
			subscription.CurrentPeriodStart,
			subscription.CurrentPeriodEnd,
			subscription.Status);
	}

	public async Task<int> QueueRenewalForAllAccountsAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
	{
		var subscriptions = await _dbContext.Subscriptions
			.Where(subscription => subscription.Status == "Active")
			.ToListAsync(cancellationToken);

		foreach (var subscription in subscriptions)
		{
			subscription.CurrentPeriodEnd = asOf;
			subscription.UpdatedAt = asOf;
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		return subscriptions.Count;
	}

	public async Task<int> TriggerImmediateRenewalForAccountAsync(Guid accountId, DateTimeOffset asOf, CancellationToken cancellationToken = default)
	{
		var subscription = await _dbContext.Subscriptions
			.SingleOrDefaultAsync(item => item.AccountId == accountId && item.Status == "Active", cancellationToken)
			?? throw new InvalidOperationException($"Active subscription for account '{accountId}' was not found.");

		subscription.CurrentPeriodEnd = asOf;
		subscription.UpdatedAt = asOf;
		await _dbContext.SaveChangesAsync(cancellationToken);

		return await ProcessDueRenewalsForAccountAsync(accountId, asOf, cancellationToken);
	}

	public async Task<int> ProcessDueRenewalsForAccountAsync(Guid accountId, DateTimeOffset asOf, CancellationToken cancellationToken = default)
	{
		var dueSubscriptions = await _dbContext.Subscriptions
			.Where(subscription => subscription.Status == "Active"
				&& subscription.AccountId == accountId
				&& subscription.CurrentPeriodEnd <= asOf)
			.ToListAsync(cancellationToken);

		if (dueSubscriptions.Count == 0)
		{
			return 0;
		}

		return await ProcessRenewalsAsync(dueSubscriptions, asOf, cancellationToken);
	}

	public async Task<int> ProcessDueRenewalsAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
	{
		var dueSubscriptions = (await _dbContext.Subscriptions
				.Where(subscription => subscription.Status == "Active")
				.ToListAsync(cancellationToken))
			.Where(subscription => subscription.CurrentPeriodEnd <= asOf)
			.OrderBy(subscription => subscription.CurrentPeriodEnd)
			.ToList();

		if (dueSubscriptions.Count == 0)
		{
			return 0;
		}

		return await ProcessRenewalsAsync(dueSubscriptions, asOf, cancellationToken);
	}

	private async Task<int> ProcessRenewalsAsync(List<Subscription> dueSubscriptions, DateTimeOffset asOf, CancellationToken cancellationToken)
	{
		var planIds = dueSubscriptions.Select(subscription => subscription.PlanId).Distinct().ToList();
		await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

		var plansById = await _dbContext.Plans
			.Where(plan => planIds.Contains(plan.Id))
			.ToDictionaryAsync(plan => plan.Id, cancellationToken);

		foreach (var subscription in dueSubscriptions)
		{
			if (!plansById.TryGetValue(subscription.PlanId, out var plan))
			{
				throw new InvalidOperationException($"Plan '{subscription.PlanId}' was not found for subscription '{subscription.Id}'.");
			}

			var currentBalance = await CreditLedgerService.GetBalanceAsync(
				_dbContext.CreditLedgerEntries,
				subscription.AccountId,
				cancellationToken);

			if (!plan.AllowsRollover && currentBalance > 0)
			{
				_dbContext.CreditLedgerEntries.Add(new CreditLedgerEntry
				{
					Id = Guid.NewGuid(),
					AccountId = subscription.AccountId,
					Amount = -currentBalance,
					Type = CreditLedgerEntryType.Expire,
					Source = CreditLedgerEntrySource.SubscriptionRenewal,
					IdempotencyKey = BuildRenewalIdempotencyKey("expire", subscription.AccountId, subscription.CurrentPeriodEnd),
					ReferenceId = subscription.Id.ToString(),
					Description = $"Expired unused credits for period ending {subscription.CurrentPeriodEnd:yyyy-MM-dd}",
					CreatedAt = asOf
				});
			}

			_dbContext.CreditLedgerEntries.Add(new CreditLedgerEntry
			{
				Id = Guid.NewGuid(),
				AccountId = subscription.AccountId,
				Amount = plan.MonthlyCreditAllowance,
				Type = CreditLedgerEntryType.Grant,
				Source = CreditLedgerEntrySource.SubscriptionRenewal,
				IdempotencyKey = BuildRenewalIdempotencyKey("grant", subscription.AccountId, subscription.CurrentPeriodEnd),
				ReferenceId = subscription.Id.ToString(),
				Description = $"{plan.Name} renewal allowance",
				CreatedAt = asOf
			});

			subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
			subscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd.AddMonths(1);
			subscription.UpdatedAt = asOf;
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return dueSubscriptions.Count;
	}

	private static string BuildRenewalIdempotencyKey(string action, Guid accountId, DateTimeOffset periodEnd)
	{
		return $"renewal-{action}-{accountId:N}-{periodEnd:yyyyMMddHHmmss}";
	}
}
