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

    public async Task<UsageEventResultDto?> SimulateUsageEventAsync(Guid accountId, string eventType, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            BuildUri("/api/simulate/usage-event"),
            new SimulateUsageEventRequestDto(accountId, eventType, idempotencyKey),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UsageEventResultDto>(cancellationToken: cancellationToken);
    }

    private Uri BuildUri(string relativePath)
    {
        return _navigation.ToAbsoluteUri(relativePath);
    }
}