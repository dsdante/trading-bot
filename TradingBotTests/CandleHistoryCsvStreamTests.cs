using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TradingBot.Data;

namespace TradingBotTests;

public class CandleHistoryCsvStreamTests
{
    private readonly string connectionString = Configuration.ConnectionString;
    private readonly TradingBotDbContext dbContext = new(
        Configuration.DbContextOptionsBuilder.Options,
        Configuration.LoggerFactory.CreateLogger<TradingBotDbContext>());

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await dbContext.Candles.ExecuteDeleteAsync();
        await dbContext.Instruments.ExecuteDeleteAsync();
        await dbContext.Instruments.AddAsync(new() { Id = 1, Name = "Test instrument" });
        await dbContext.SaveChangesAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() =>
        await dbContext.DisposeAsync();

    [SetUp]
    public async Task SetUp() =>
        await dbContext.Candles.ExecuteDeleteAsync();

    [Test]
    public async Task Write()
    {
        await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, Configuration.LoggerFactory, CancellationToken.None);
        await using var file = File.OpenRead(Path.Combine("CandleHistoryCsv", "candle.csv"));
        await file.CopyToAsync(dbStream);
        await dbStream.CommitAsync(CancellationToken.None);
        var candles = await dbContext.Candles.ToListAsync();

        Assert.That(candles, Has.One.Items);
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
