using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using TradingBot;
using TradingBot.Data;

namespace TradingBotTests;

public class TInvestHistoryDataServiceTests
{
    private readonly HttpClient httpClient = new();
    private readonly TradingBotDbContext dbContext = new(
        Configuration.DbContextOptionsBuilder.Options,
        Configuration.LoggerFactory.CreateLogger<TradingBotDbContext>());
    private TInvestHistoryDataService tInvestHistoryData;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Configuration.TInvestToken);
        tInvestHistoryData = new(
            httpClient,
            Configuration.ConnectionStringBuilder,
            Configuration.LoggerFactory,
            Configuration.LoggerFactory.CreateLogger<TInvestHistoryDataService>());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        httpClient.Dispose();
        await dbContext.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await dbContext.Candles.ExecuteDeleteAsync();
        await dbContext.Instruments.ExecuteDeleteAsync();
    }

    [Test]
    public async Task DownloadCsv()
    {
        var instrument = new Instrument
        {
            Name = "NVIDIA",
            Figi = "BBG000BBJQV0",
        };
        dbContext.Instruments.Add(instrument);
        await dbContext.SaveChangesAsync();

        await tInvestHistoryData.DownloadCsvAsync(instrument, 2020, CancellationToken.None);
        var candles = await dbContext.Candles.ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(candles, Has.Exactly(193288).Items);
            Assert.That(candles, Is.All.Matches<Candle>(candle => candle.Instrument.Id == instrument.Id));
            Assert.That(candles, Is.All.Matches<Candle>(candle => candle.Low > 0));
            Assert.That(candles, Is.All.Matches<Candle>(candle => candle.Low <= candle.Open));
            Assert.That(candles, Is.All.Matches<Candle>(candle => candle.Low <= candle.Close));
            Assert.That(candles, Is.All.Matches<Candle>(candle => candle.High >= candle.Open));
            Assert.That(candles, Is.All.Matches<Candle>(candle => candle.High >= candle.Close));
            Assert.That(candles, Is.All.Matches<Candle>(candle => candle.Volume >= 0));
        });
    }
}
