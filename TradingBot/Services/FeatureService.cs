using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.Data;
namespace TradingBot;

/// <summary> Feature engineering </summary>
public partial class FeatureService(
    TradingBotDbContext dbContext,
    IOptions<TradingBotOptions> options,
    ILogger<HistoryService> logger)
{
    /// <summary> Calculate the mean and the standard deviation of the features </summary>
    /// <remarks>Should be run once and updated in the configuration manually.</remarks>
    public async Task<TradingBotOptions.FeatureScaleOptions> GetScaleAsync(CancellationToken cancellation)
    {
        logger.LogInformation("Calculating the mean and the standard deviation of the features...");

        // TODO: Replace with Linq once LAG() is supported in EF. https://github.com/dotnet/efcore/issues/12747
        var scale = await dbContext.Database
            .SqlQuery<TradingBotOptions.FeatureScaleOptions>($"""
                SELECT
                    AVG(lag) AS lag_mean,
                    stddev(lag) AS lag_deviation,
                    AVG(gap) AS gap_mean,
                    stddev(gap) AS gap_deviation,
                    AVG(volume) AS volume_mean,
                    stddev(volume) AS volume_deviation
                FROM (
                    SELECT
                        instrument,
                        timestamp,
                        LN(timestamp - LAG(timestamp) OVER (PARTITION BY instrument ORDER BY timestamp)) AS lag,
                        LN(close / LAG(close) OVER (PARTITION BY instrument ORDER BY timestamp)) AS gap,
                        LN(volume * lot * SQRT(low * high) + 1) AS volume
                    FROM candle
                    JOIN instrument ON instrument.id = instrument
                    WHERE
                        api_trade_available
                        AND low > 0
                        AND asset_type = ANY({options.Value.AssetTypes})
                        AND country = ANY({options.Value.Countries})
                )
                WHERE lag IS NOT NULL
                """)
            .SingleAsync(cancellation);

        LogFeatureMeanAndStandardDeviation(scale);

        return scale;
    }

    /// <summary> Calculate and save features from new candles </summary>
    public async Task<int> UpdateFeaturesAsync(CancellationToken cancellation)
    {
        logger.LogInformation("Updating the features...");
        var scale = options.Value.FeatureScale;

        // TODO: Replace with Linq once LAG() is supported in EF. https://github.com/dotnet/efcore/issues/12747
        // TODO: This request takes ~1:50:00; add a timeout of 10800 for it.
        var addedCount = await dbContext.Database.ExecuteSqlAsync($"""
            WITH feature_unscaled AS (
                SELECT
                    instrument,
                    timestamp,
                    LN(timestamp - LAG(timestamp) OVER (PARTITION BY instrument ORDER BY timestamp)) AS lag,
                    LN(close / LAG(close) OVER (PARTITION BY instrument ORDER BY timestamp)) AS gap,
                    LN(volume * lot * SQRT(low * high) + 1) AS volume
                FROM candle
                JOIN instrument ON instrument.id = instrument
                WHERE
                    api_trade_available
                    AND low > 0
                    AND asset_type = ANY({options.Value.AssetTypes})
                    AND country = ANY({options.Value.Countries})
            )

            INSERT INTO feature
            SELECT
                instrument,
                timestamp,
                (lag - {scale.LagMean}) / {scale.LagDeviation} AS lag,
                (gap - {scale.GapMean}) / {scale.GapDeviation} AS gap,
                (volume - {scale.VolumeMean}) / {scale.VolumeDeviation} AS volume
            FROM feature_unscaled
            WHERE lag IS NOT NULL
            ON CONFLICT DO NOTHING
            """, cancellation);

        if (addedCount > 0)
            LogFeaturesAdded(addedCount);
        else
            logger.LogInformation("All features are up to date.");

        return addedCount;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = @"{scale}")]
    private partial void LogFeatureMeanAndStandardDeviation(TradingBotOptions.FeatureScaleOptions scale);

    [LoggerMessage(Level = LogLevel.Information, Message = @"{count} feature tuples added.")]
    private partial void LogFeaturesAdded(int count);
}
