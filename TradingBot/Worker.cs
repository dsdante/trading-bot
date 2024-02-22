using Microsoft.EntityFrameworkCore;
using TradingBot.Data;

namespace TradingBot;

public class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
            var x = await db.Instruments
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellation);
            logger.LogInformation("{instrument}", x?.Name);
        }

        while (!cancellation.IsCancellationRequested)
        {
            logger.LogInformation("Worker running.");
            await Task.Delay(1000, cancellation);
        }
    }
}
