using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TradingBot;
using TradingBot.Data;

namespace TradingBotTests;

public class HistoryServiceTests
{
    private readonly TInvestServiceMock tInvestService = new();
    private readonly TInvestHistoryDataServiceMock tInvestHistoryData = new();
    private readonly TradingBotDbContext dbContext = new(
        Configuration.DbContextOptionsBuilder.Options,
        Configuration.LoggerFactory.CreateLogger<TradingBotDbContext>());
    private HistoryService history;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        history = new(tInvestService,
                      tInvestHistoryData,
                      dbContext,
                      Configuration.LoggerFactory.CreateLogger<HistoryService>());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() =>
        await dbContext.DisposeAsync();

    [SetUp]
    public async Task SetUp()
    {
        tInvestService.Reset();
        tInvestHistoryData.Reset();

        await dbContext.Candles.ExecuteDeleteAsync();
        await dbContext.Instruments.ExecuteDeleteAsync();
    }

    [Test]
    public async Task UpdateInstruments()
    {
        await history.UpdateInstrumentsAsync(CancellationToken.None);

        var expected = tInvestService.GetInstrumentsAsync().ToBlockingEnumerable();
        var actual = await dbContext.Instruments.ToListAsync();

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task DownloadHistoryBeginning()
    {
        var instrument = tInvestService.DefaultInstrument;
        dbContext.Instruments.Add(instrument);
        await dbContext.SaveChangesAsync();
        await history.DownloadHistoryBeginningAsync(CancellationToken.None);

        // Without the current year, but plus one for the 404 check.
        var expected = Enumerable.Range(DateTime.UtcNow.Year - tInvestHistoryData.HistoryYears, tInvestHistoryData.HistoryYears)
            .Reverse()
            .Select(year => (instrument, year));
        var actual = tInvestHistoryData.Requests;

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task DownloadHistoryBeginningFailedFirstChance()
    {
        var instrument = tInvestService.DefaultInstrument;
        dbContext.Instruments.Add(instrument);
        await dbContext.SaveChangesAsync();
        int thisYear = DateTime.UtcNow.Year;
        tInvestHistoryData.HistoryYears = 4;
        tInvestHistoryData.YearServerErrors.Add(thisYear - 2);
        await history.DownloadHistoryBeginningAsync(CancellationToken.None);

        var expected = new[] { thisYear - 1, thisYear - 2, thisYear - 2, thisYear - 3, thisYear - 4 }
            .Select(year => (instrument, year));
        var actual = tInvestHistoryData.Requests;

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task DownloadHistoryBeginningFailedSecondChance()
    {
        var instrument = tInvestService.DefaultInstrument;
        dbContext.Instruments.Add(instrument);
        await dbContext.SaveChangesAsync();
        int thisYear = DateTime.UtcNow.Year;
        tInvestHistoryData.HistoryYears = 4;
        tInvestHistoryData.YearServerErrors.Add(thisYear - 2);
        tInvestHistoryData.YearServerErrors.Add(thisYear - 2);
        await history.DownloadHistoryBeginningAsync(CancellationToken.None);

        var expected = new[] { thisYear - 1, thisYear - 2, thisYear - 2 }
            .Select(year => (instrument, year));
        var actual = tInvestHistoryData.Requests;

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task UpdateHistory()
    {
        int thisYear = DateTime.UtcNow.Year;
        DateTime lastYear = new(thisYear - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var instrument = tInvestService.DefaultInstrument;
        dbContext.Instruments.Add(instrument);
        dbContext.Candles.Add(new Candle { Instrument = instrument, Timestamp = lastYear });
        await dbContext.SaveChangesAsync();
        await history.UpdateHistoryAsync(CancellationToken.None);

        // Without the current year, but plus one for the 404 check.
        var expected = new[] { thisYear - 1, thisYear }
            .Select(year => (instrument, year));
        var actual = tInvestHistoryData.Requests;

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task UpdateHistoryFailedFirstChance()
    {
        int thisYear = DateTime.UtcNow.Year;
        DateTime lastYear = new(thisYear - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var instrument = tInvestService.DefaultInstrument;
        dbContext.Instruments.Add(instrument);
        dbContext.Candles.Add(new Candle { Instrument = instrument, Timestamp = lastYear });
        await dbContext.SaveChangesAsync();
        tInvestHistoryData.YearServerErrors.Add(thisYear - 1);
        await history.UpdateHistoryAsync(CancellationToken.None);

        var expected = new[] { thisYear - 1, thisYear - 1, thisYear }
            .Select(year => (instrument, year));
        var actual = tInvestHistoryData.Requests;

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task UpdateHistoryFailedSecondChance()
    {
        int thisYear = DateTime.UtcNow.Year;
        DateTime lastYear = new(thisYear - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var instrument = tInvestService.DefaultInstrument;
        dbContext.Instruments.Add(instrument);
        dbContext.Candles.Add(new Candle { Instrument = instrument, Timestamp = lastYear });
        await dbContext.SaveChangesAsync();
        tInvestHistoryData.YearServerErrors.Add(thisYear - 1);
        tInvestHistoryData.YearServerErrors.Add(thisYear - 1);
        await history.UpdateHistoryAsync(CancellationToken.None);

        var expected = new[] { thisYear - 1, thisYear - 1 }
            .Select(year => (instrument, year));
        var actual = tInvestHistoryData.Requests;

        Assert.That(actual, Is.EquivalentTo(expected));
    }
}
