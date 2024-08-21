using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;
using TradingBot.Data;

namespace TradingBotTests;

public class CandleHistoryCsvStreamTests
{
    private readonly string connectionString = Configuration.ConnectionString;
    private readonly TradingBotDbContext dbContext = Configuration.DbContext;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        if (!await dbContext.Instruments.AnyAsync(instrument => instrument.Id == 1))
        {
            await dbContext.Instruments.AddAsync(new() { Id = 1, Name = "Test instrument" });
            await dbContext.SaveChangesAsync();
        }
        await dbContext.Candles.ExecuteDeleteAsync();
    }

    [TearDown]
    public async Task TearDown() =>
        await dbContext.Candles.ExecuteDeleteAsync();

    [Test]
    public async Task Write()
    {
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, Configuration.LoggerFactory, CancellationToken.None);
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "candle.csv"));
        await file.CopyToAsync(dbStream);
        await dbStream.CommitAsync(CancellationToken.None);
        var count = await dbContext.Candles.CountAsync();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task WriteAfterCommit()
    {
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, Configuration.LoggerFactory, CancellationToken.None);
        await dbStream.CommitAsync(CancellationToken.None);
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "candle.csv"));
        // "The COPY operation has already ended."
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await file.CopyToAsync(dbStream));
    }

    [Test]
    public async Task MalformattedCsv()
    {
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, Configuration.LoggerFactory, CancellationToken.None);
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "malformatted.csv"));
        await file.CopyToAsync(dbStream);
        // "22P04: extra data after last expected column"
        Assert.ThrowsAsync<PostgresException>(async () => await dbStream.CommitAsync(CancellationToken.None));
    }
}
