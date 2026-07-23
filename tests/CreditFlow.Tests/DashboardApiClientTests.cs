using System.Net;
using System.Net.Http.Json;
using CreditFlow.Components.Services;
using CreditFlow.Contracts;
using Microsoft.AspNetCore.Components;

namespace CreditFlow.Tests;

public class DashboardApiClientTests
{
    [Fact]
    public async Task GetAccountsAsync_ReturnsAccountsFromApi()
    {
        var expected = new List<AccountSummaryDto>
        {
            new(Guid.NewGuid(), "Acme", "acme@example.com")
        };

        var sut = BuildClient(async request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/accounts", request.RequestUri?.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            };
        });

        var result = await sut.GetAccountsAsync();

        Assert.Single(result);
        Assert.Equal(expected[0].Id, result[0].Id);
    }

    [Fact]
    public async Task GetBalanceAsync_ReturnsNullWhenAccountIsNotFound()
    {
        var accountId = Guid.NewGuid();

        var sut = BuildClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await sut.GetBalanceAsync(accountId);

        Assert.Null(result);
    }

    [Fact]
    public async Task SimulateUsageEventAsync_PostsExpectedPayload()
    {
        var accountId = Guid.NewGuid();
        var expected = new UsageEventResultDto(Guid.NewGuid(), accountId, "model-inference", 25, "abc-123", 75);

        var sut = BuildClient(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/simulate/usage-event", request.RequestUri?.AbsolutePath);

            var payload = await request.Content!.ReadFromJsonAsync<SimulateUsageEventRequestDto>();
            Assert.NotNull(payload);
            Assert.Equal(accountId, payload.AccountId);
            Assert.Equal("model-inference", payload.EventType);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            };
        });

        var result = await sut.SimulateUsageEventAsync(accountId, "model-inference", null);

        Assert.NotNull(result);
        Assert.Equal(expected.UsageEventId, result.UsageEventId);
        Assert.Equal(expected.Balance, result.Balance);
    }

    private static DashboardApiClient BuildClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var navigation = new TestNavigationManager();
        var httpClient = new HttpClient(new DelegateHandler(handler));
        return new DashboardApiClient(httpClient, navigation);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/");
        }
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
