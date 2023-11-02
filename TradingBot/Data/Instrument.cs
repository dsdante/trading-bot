using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Data;

public class Instrument
{
    public int Id { get; set; }
    public Guid Uid { get; set; }
    public string? Figi { get; set; }
    public required string Name { get; set; }
    public AssetType AssetType { get; set; }
    public int Lot { get; set; }
    [Column("otc_flag")] public bool Otc { get; set; }
    [Column("for_qual_investor_flag")] public bool ForQualInvestor { get; set; }
    [Column("api_trade_available_flag")] public bool ApiTradeAvailable { get; set; }
    public DateTime? First1MinCandleDate { get; set; }
    public DateTime? First1DayCandleDate { get; set; }
}
