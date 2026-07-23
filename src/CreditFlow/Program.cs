using CreditFlow.Application;
using CreditFlow.Components;
using CreditFlow.Contracts;
using CreditFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CreditFlowDb")
    ?? throw new InvalidOperationException("Connection string 'CreditFlowDb' is not configured.");

builder.Services.AddDbContext<CreditFlowDbContext>(options =>
    options.UseSqlite(connectionString));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<AccountQueryService>();
builder.Services.AddScoped<UsageService>();

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
        catch (InvalidOperationException)
        {
            return Results.NotFound();
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
