using System.Net;
using System.Net.Http.Json;
using CreditFlow.Contracts;
using Microsoft.AspNetCore.Components;

namespace CreditFlow.Components.Services;

public class DashboardApiClient
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigation;

    public DashboardApiClient(HttpClient httpClient, NavigationManager navigation)
    {
        _httpClient = httpClient;
        _navigation = navigation;
    }

    public async Task<List<AccountSummaryDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _httpClient.GetFromJsonAsync<List<AccountSummaryDto>>(
            BuildUri("/api/accounts"),
            cancellationToken);

        return accounts ?? [];
    }

    public async Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(BuildUri($"/api/accounts/{accountId}/balance"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountBalanceDto>(cancellationToken: cancellationToken);
    }

    public async Task<List<PlanDto>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _httpClient.GetFromJsonAsync<List<PlanDto>>(
            BuildUri("/api/plans"),
            cancellationToken);

        return plans ?? [];
    }

    public async Task<SubscriptionDto?> GetSubscriptionAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(BuildUri($"/api/accounts/{accountId}/subscription"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubscriptionDto>(cancellationToken: cancellationToken);
    }

    public async Task<SubscriptionDto?> UpdatePlanAsync(Guid accountId, Guid planId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            BuildUri($"/api/accounts/{accountId}/plan"),
            new UpdateAccountPlanRequestDto(planId),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubscriptionDto>(cancellationToken: cancellationToken);
    }

    public async Task<RenewalActionResultDto?> QueueRenewalForAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            BuildUri("/api/simulate/renewals/queue-all"),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RenewalActionResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<RenewalActionResultDto?> RenewSelectedAccountNowAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            BuildUri($"/api/simulate/accounts/{accountId}/renew-now"),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RenewalActionResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<UsageEventResultDto?> SimulateUsageEventAsync(Guid accountId, string eventType, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            BuildUri("/api/simulate/usage-event"),
            new SimulateUsageEventRequestDto(accountId, eventType, idempotencyKey),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UsageEventResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<PaymentWebhookResultDto?> SimulatePaymentWebhookAsync(Guid accountId, int? credits, string? idempotencyKey, string? description, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            BuildUri("/api/simulate/payment-webhook"),
            new SimulatePaymentWebhookRequestDto(accountId, credits, idempotencyKey, description),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken: cancellationToken);
    }

    private Uri BuildUri(string relativePath)
    {
        return _navigation.ToAbsoluteUri(relativePath);
    }
}