using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using TradingBot.Data;

namespace TradingBotTests;

public class CandleHistoryCsvStreamTests : IAsyncDisposable
{
    private string connectionString = null!;
    private TradingBotDbContext dbContext = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        connectionString = Configuration.ConnectionString;
        var logger = Configuration.LoggerFactory.CreateLogger<TradingBotDbContext>();
        var optionsBuilder = new DbContextOptionsBuilder<TradingBotDbContext>()
            .UseNpgsql(Configuration.DataSource);
        dbContext = new TradingBotDbContext(optionsBuilder.Options, logger);
        await dbContext.Candles.ExecuteDeleteAsync();
    }

    [TearDown]
    public async Task TearDown() =>
        await dbContext.Candles.ExecuteDeleteAsync();

    [Test]
    public async Task Write()
    {
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, CancellationToken.None);
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "candle.csv"));
        await file.CopyToAsync(dbStream);
        await dbStream.CommitAsync(CancellationToken.None);
        var count = await dbContext.Candles.CountAsync();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task WriteAfterCommit()
    {
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, CancellationToken.None);
        await dbStream.CommitAsync(CancellationToken.None);
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "candle.csv"));
        // "The COPY operation has already ended."
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await file.CopyToAsync(dbStream));
    }

    [Test]
    public async Task MalformattedCsv()
    {
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, CancellationToken.None);
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "malformatted.csv"));
        await file.CopyToAsync(dbStream);
        // "22P04: extra data after last expected column"
        Assert.ThrowsAsync<PostgresException>(async () => await dbStream.CommitAsync(CancellationToken.None));
    }

    public async ValueTask DisposeAsync() =>
        await dbContext.DisposeAsync();
}
