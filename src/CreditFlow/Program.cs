using CreditFlow.Application;
using CreditFlow.Components;
using CreditFlow.Components.Services;
using CreditFlow.Contracts;
using CreditFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CreditFlowDb")
    ?? throw new InvalidOperationException("Connection string 'CreditFlowDb' is not configured.");

builder.Services.AddDbContext<CreditFlowDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.Configure<BillingWebhookOptions>(builder.Configuration.GetSection(BillingWebhookOptions.SectionName));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<AccountQueryService>();
builder.Services.AddScoped<UsageService>();
builder.Services.AddScoped<BillingWebhookService>();
builder.Services.AddHttpClient<DashboardApiClient>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CreditFlowDbContext>();
    try

    {

        await dbContext.Database.MigrateAsync();

    }

    catch (Exception ex)

    {

        app.Logger.LogError(ex, "Failed to apply database migrations at startup.");

        throw;

    }

}

app.MapGet("/api/accounts", async (AccountQueryService queryService) =>
{
    return Results.Ok(await queryService.GetAccountsAsync());
});

app.MapGet("/api/accounts/{id:guid}/balance", async (Guid id, AccountQueryService queryService) =>
{
    var balance = await queryService.GetBalanceAsync(id);
    if (balance is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(balance);
});

app.MapPost("/api/accounts/{id:guid}/usage-events", async (Guid id, UsageEventRequestDto request, UsageService usageService, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await usageService.RecordUsageEvent(id, request.EventType, request.IdempotencyKey, cancellationToken);
        return Results.Ok(new UsageEventResultDto(
            result.UsageEventId,
            result.AccountId,
            result.EventType,
            result.CreditCost,
            result.IdempotencyKey,
            result.Balance));
    }
    catch (InsufficientCreditsException ex)
    {
        return Results.BadRequest(new { error = ex.Message, availableCredits = ex.AvailableCredits, requiredCredits = ex.RequiredCredits });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});

app.MapPost("/api/webhooks/billing", async (HttpRequest request, BillingWebhookService billingWebhookService, CancellationToken cancellationToken) =>
{
    string payload;
    using (var reader = new StreamReader(request.Body))
    {
        payload = await reader.ReadToEndAsync(cancellationToken);
    }

    var signature = request.Headers["X-CreditFlow-Signature"].FirstOrDefault() ?? string.Empty;

    try
    {
        var result = await billingWebhookService.ProcessPaymentEvent(payload, signature, cancellationToken);
        return Results.Ok(new PaymentWebhookResultDto(
            result.AccountId,
            result.CreditsGranted,
            result.IdempotencyKey,
            result.ReferenceId,
            result.Balance,
            result.AlreadyProcessed));
    }
    catch (InvalidWebhookSignatureException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
    catch (MalformedWebhookPayloadException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/simulate/usage-event", async (SimulateUsageEventRequestDto request, UsageService usageService, CancellationToken cancellationToken) =>
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"simulate-usage-{Guid.NewGuid():N}"
            : request.IdempotencyKey;

        try
        {
            var result = await usageService.RecordUsageEvent(request.AccountId, request.EventType, idempotencyKey, cancellationToken);
            return Results.Ok(new UsageEventResultDto(
                result.UsageEventId,
                result.AccountId,
                result.EventType,
                result.CreditCost,
                result.IdempotencyKey,
                result.Balance));
        }
        catch (InsufficientCreditsException ex)
        {
            return Results.BadRequest(new { error = ex.Message, availableCredits = ex.AvailableCredits, requiredCredits = ex.RequiredCredits });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPost("/api/simulate/payment-webhook", async (SimulatePaymentWebhookRequestDto request, BillingWebhookService billingWebhookService, IOptions<BillingWebhookOptions> options, CancellationToken cancellationToken) =>
    {
        if (request.AccountId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "AccountId is required." });
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"simulate-payment-{Guid.NewGuid():N}"
            : request.IdempotencyKey;

        var payloadDto = new PaymentWebhookPayloadDto(
            request.AccountId,
            request.Credits.GetValueOrDefault(100),
            idempotencyKey,
            $"simulated-payment-{Guid.NewGuid():N}",
            string.IsNullOrWhiteSpace(request.Description) ? "Simulated payment webhook" : request.Description);

        var payload = JsonSerializer.Serialize(payloadDto);
        var signature = BillingWebhookService.CreateSignature(payload, options.Value.SigningSecret);

        try
        {
            var result = await billingWebhookService.ProcessPaymentEvent(payload, signature, cancellationToken);
            return Results.Ok(new PaymentWebhookResultDto(
                result.AccountId,
                result.CreditsGranted,
                result.IdempotencyKey,
                result.ReferenceId,
                result.Balance,
                result.AlreadyProcessed));
        }
        catch (MalformedWebhookPayloadException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    });
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
