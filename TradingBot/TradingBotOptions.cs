using TradingBot.Data;

namespace TradingBot;

public class TradingBotOptions
{
    public record FeatureScaleOptions(
        float LagMean,
        float LagDeviation,
        float GapMean,
        float GapDeviation,
        float VolumeMean,
        float VolumeDeviation);

    public required AssetType[] AssetTypes { get; init; }
    public required FeatureScaleOptions FeatureScale { get; init; }
}
