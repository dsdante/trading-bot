using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingBot;
using TradingBot.Data;

namespace TradingBotTests;

public class FeatureServiceTests
{
    private readonly TradingBotDbContext dbContext = new(
        Configuration.DbContextOptionsBuilder.Options,
        Configuration.LoggerFactory.CreateLogger<TradingBotDbContext>());

    private FeatureService featureService;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await dbContext.Feature.ExecuteDeleteAsync();
        await dbContext.Candles.ExecuteDeleteAsync();
        await dbContext.Instruments.ExecuteDeleteAsync();

        var nvda = await dbContext.Instruments.AddAsync(new Instrument
        {
            AssetType = AssetType.Share,
            Name = "NVIDIA",
            Ticker = "NVDA",
            Figi = "BBG000BBJQV0",
            Uid = new("81575098-df8a-45c4-82dc-1b64374dcfdb"),
            Lot = 1,
            Country = "US",
            Otc = false,
            Qual = true,
            ApiTradeAvailable = true,
        });

        var rosn = await dbContext.Instruments.AddAsync(new Instrument
        {
            AssetType = AssetType.Share,
            Name = "Роснефть",
            Ticker = "ROSN",
            Figi = "BBG004731354",
            Uid = new("fd417230-19cf-4e7b-9623-f7c9ca18ec6b"),
            Lot = 1,
            Country = "RU",
            Otc = false,
            Qual = false,
            ApiTradeAvailable = true,
        });

        static IEnumerable<Candle> LoadCandles(Instrument instrument, string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                var fields = line.Split(';');

                yield return new Candle
                {
                    Instrument = instrument,
                    Timestamp = DateTime.Parse(fields[1]).ToUniversalTime(),
                    Open = float.Parse(fields[2]),
                    Close = float.Parse(fields[3]),
                    High = float.Parse(fields[4]),
                    Low = float.Parse(fields[5]),
                    Volume = long.Parse(fields[6]),
                };
            }
        }

        var nvdaCandles = LoadCandles(nvda.Entity, Path.Combine("CandleHistoryCsv", "NVDA_2020-01-03.csv"));
        var rosnCandles = LoadCandles(rosn.Entity, Path.Combine("CandleHistoryCsv", "ROSN_2020-01-03.csv"));
        await dbContext.Candles.AddRangeAsync(nvdaCandles);
        await dbContext.Candles.AddRangeAsync(rosnCandles);

        await dbContext.SaveChangesAsync();

        featureService = new(
            dbContext,
            Configuration.TradingBotOptions,
            Configuration.LoggerFactory.CreateLogger<HistoryService>());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() =>
        await dbContext.DisposeAsync();

    [SetUp]
    public async Task SetUp() =>
        await dbContext.Feature.ExecuteDeleteAsync();

    [Test]
    public async Task GetScale()
    {
        var actual = await featureService.GetScaleAsync(CancellationToken.None);
        var expected = Configuration.TradingBotOptions.Value.FeatureScale;
        var delta = 1e-6;

        Assert.Multiple(() =>
        {
            Assert.That(actual.LagMean, Is.EqualTo(expected.LagMean).Within(delta));
            Assert.That(actual.LagDeviation, Is.EqualTo(expected.LagDeviation).Within(delta));
            Assert.That(actual.GapMean, Is.EqualTo(expected.GapMean).Within(delta));
            Assert.That(actual.GapDeviation, Is.EqualTo(expected.GapDeviation).Within(delta));
            Assert.That(actual.VolumeMean, Is.EqualTo(expected.VolumeMean).Within(delta));
            Assert.That(actual.VolumeDeviation, Is.EqualTo(expected.VolumeDeviation).Within(delta));
        });
    }

    [Test]
    public async Task UpdateFeatures()
    {
        // Missing one feature tuple for each intrument due to the LAG() function, so features = candles - instruments.
        int instrumentCount = await dbContext.Instruments.CountAsync();
        int candleCount = await dbContext.Candles.CountAsync();
        var addedCount = await featureService.UpdateFeaturesAsync(CancellationToken.None);
        Assert.That(addedCount, Is.EqualTo(candleCount - instrumentCount));

        var features = await dbContext.Feature.ToListAsync();

        var lagMean = features.Average(f => f.Lag);
        var gapMean = features.Average(f => f.Gap);
        var volumeMean = features.Average(f => f.Volume);
        var lagDeviation = StdDev(features.Select(f => (double)f.Lag));
        var gapDeviation = StdDev(features.Select(f => (double)f.Gap));
        var volumeDeviation = StdDev(features.Select(f => (double)f.Volume));

        var delta = 1e-6;

        Assert.Multiple(() =>
        {
            Assert.That(lagMean, Is.Zero.Within(delta));
            Assert.That(gapMean, Is.Zero.Within(delta));
            Assert.That(volumeMean, Is.Zero.Within(delta));
            Assert.That(lagDeviation, Is.EqualTo(1).Within(delta));
            Assert.That(gapDeviation, Is.EqualTo(1).Within(delta));
            Assert.That(volumeDeviation, Is.EqualTo(1).Within(delta));
        });
    }

    // Welford's method for higher precision https://stackoverflow.com/a/2878000
    private static double StdDev(IEnumerable<double> source)
    {
        double mean = 0;
        double sum = 0;
        int n = 0;

        foreach (var value in source)
        {
            var delta = value - mean;
            mean += delta / ++n;
            sum += delta * (value - mean);
        }

        return n > 1 ? Math.Sqrt(sum / (n - 1)) : 0;
    }
}
