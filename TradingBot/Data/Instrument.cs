using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Data;

[Index(nameof(Uid), IsUnique = true)]
public class Instrument : IEquatable<Instrument>
{
    public short Id { get; init; }
    public AssetType AssetType { get; set; }
    public required string Name { get; set; }
    public string? Ticker { get; set; }
    public string? Figi { get; set; }
    public Guid Uid { get; init; }
    public int Lot { get; set; }
    [Column("otc_flag")] public bool Otc { get; set; }
    [Column("for_qual_investor_flag")] public bool ForQualInvestor { get; set; }
    [Column("api_trade_available_flag")] public bool ApiTradeAvailable { get; set; }
    [Column("has_earliest_1min_candle")] public bool HasEarliest1MinCandle { get; set; }

    public ICollection<Candle> Candles { get; init; } = null!;

    public override string ToString() => Name;

    public bool Equals(Instrument? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        // Compare all properties except the auto-generated Id.
        return
            AssetType == other.AssetType &&
            Name == other.Name &&
            Ticker == other.Ticker &&
            Figi == other.Figi &&
            Uid == other.Uid &&
            Lot == other.Lot &&
            Otc == other.Otc &&
            ForQualInvestor == other.ForQualInvestor &&
            ApiTradeAvailable == other.ApiTradeAvailable;
    }

    public override bool Equals(object? obj) => Equals(obj as Instrument);

    public static bool operator ==(Instrument? a, Instrument? b)
    {
        if (a is null)
            return b is null;
        return a.Equals(b);
    }

    public static bool operator !=(Instrument? a, Instrument? b) => !(a == b);

    public override int GetHashCode()
    {
        HashCode hashCode = new();
        hashCode.Add(AssetType);
        hashCode.Add(Name);
        hashCode.Add(Ticker);
        hashCode.Add(Figi);
        hashCode.Add(Uid);
        hashCode.Add(Lot);
        hashCode.Add(Otc);
        hashCode.Add(ForQualInvestor);
        hashCode.Add(ApiTradeAvailable);
        return hashCode.ToHashCode();
    }
}
