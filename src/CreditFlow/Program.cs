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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CreditFlowDbContext>();
    dbContext.Database.Migrate();
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
