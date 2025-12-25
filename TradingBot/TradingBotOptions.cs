using TradingBot.Data;

namespace TradingBot;

public class TradingBotOptions
{
    public record FeatureScaleOptions(
        double LagMean,
        double LagDeviation,
        double GapMean,
        double GapDeviation,
        double VolumeMean,
        double VolumeDeviation);

    public required AssetType[] AssetTypes { get; init; }
    public required string[] Countries { get; init; }
    public required FeatureScaleOptions FeatureScale { get; init; }
    public required string CacheDirectory
    {
        get;
        init
        {
            field = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
            Directory.CreateDirectory(field);
        }
    }
}
