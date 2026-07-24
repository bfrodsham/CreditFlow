using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CreditFlow.Application;

public class PlanRenewalJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlanRenewalJob> _logger;
    private readonly TimeSpan _interval;

    public PlanRenewalJob(
        IServiceScopeFactory scopeFactory,
        IOptions<PlanRenewalOptions> options,
        ILogger<PlanRenewalJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var configuredInterval = options.Value.IntervalSeconds;
        _interval = TimeSpan.FromSeconds(configuredInterval <= 0 ? 60 : configuredInterval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var planService = scope.ServiceProvider.GetRequiredService<PlanService>();

                var renewedSubscriptions = await planService.ProcessDueRenewalsAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (renewedSubscriptions > 0)
                {
                    _logger.LogInformation("Processed {RenewedSubscriptions} subscription renewals.", renewedSubscriptions);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing subscription renewals.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}