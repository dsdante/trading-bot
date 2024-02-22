using Microsoft.EntityFrameworkCore;
using Tinkoff.InvestApi;
using TradingBot.Data;

namespace TradingBot;

public class Worker(InvestApiClient tinkoff,
                    IServiceScopeFactory scopeFactory,
                    ILogger<Worker> logger,
                    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        try
        {
            var shares = await tinkoff.Instruments.SharesAsync(cancellation);
            logger.LogInformation("{instrument}", shares.Instruments.FirstOrDefault()?.Name);

            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
                var x = await db.Instruments
                    .OrderBy(instrument => instrument.Id)
                    .Select(instrument => instrument.Name)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellation);
                logger.LogInformation("{instrument}", x);
            }

            lifetime.StopApplication();
        }
        catch (Exception e) when (e.InnerException is OperationCanceledException)  // Tinkoff API fix
        {
            throw e.InnerException;
        }
    }
}
