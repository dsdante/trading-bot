using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TradingBot.Data;

namespace TradingBotTests;

public class CandleHistoryTests : IAsyncDisposable
{
    private string connectionString = null!;
    private TradingBotDbContext dbContext = null!;

    [SetUp]
    public void SetUp()
    {
        connectionString = Configuration.ConnectionString;
        var logger = Configuration.LoggerFactory.CreateLogger<TradingBotDbContext>();
        var optionsBuilder = new DbContextOptionsBuilder<TradingBotDbContext>()
            .UseNpgsql(Configuration.DataSource);
        dbContext = new TradingBotDbContext(optionsBuilder.Options, logger);
    }

    [Test]
    public async Task SimpleAsync()
    {
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "candle.csv"));
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, CancellationToken.None);
        await file.CopyToAsync(dbStream);
        await dbStream.CommitAsync(CancellationToken.None);

        var count = await dbContext.Candles.CountAsync();
        Assert.That(count, Is.EqualTo(1));
    }

    public async ValueTask DisposeAsync() =>
        await dbContext.DisposeAsync();
}
