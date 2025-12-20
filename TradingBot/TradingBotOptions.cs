using TradingBot.Data;

namespace TradingBot;

public class TradingBotOptions
{
    public required AssetType[] AssetTypes { get; init; }
}
