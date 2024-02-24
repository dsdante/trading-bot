using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace TradingBot.Data;

[SuppressMessage("ReSharper", "EntityFramework.ModelValidation.UnlimitedStringLength")]
[Index(nameof(Uid), IsUnique = true)]
public class Instrument : IEquatable<Instrument>
{
    public short Id { get; init; }
    public Guid Uid { get; init; }
    public string? Figi { get; set; }
    public required string Name { get; set; }
    public AssetType AssetType { get; set; }
    public int Lot { get; set; }
    [Column("otc_flag")] public bool Otc { get; set; }
    [Column("for_qual_investor_flag")] public bool ForQualInvestor { get; set; }
    [Column("api_trade_available_flag")] public bool ApiTradeAvailable { get; set; }
    public DateTime? First1MinCandleDate { get; set; }
    public DateTime? First1DayCandleDate { get; set; }

    public bool Equals(Instrument? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        // Compare all properties except the auto-generated Id.
        return Uid == other.Uid &&
               Figi == other.Figi &&
               Name == other.Name &&
               AssetType == other.AssetType &&
               Lot == other.Lot &&
               Otc == other.Otc &&
               ForQualInvestor == other.ForQualInvestor &&
               ApiTradeAvailable == other.ApiTradeAvailable &&
               First1MinCandleDate == other.First1MinCandleDate &&
               First1DayCandleDate == other.First1DayCandleDate;
    }

    public override bool Equals(object? obj) =>
        obj is Instrument other && Equals(other);

    public static bool operator ==(Instrument? a, Instrument? b)
    {
        if (a is null)
            return b is null;
        return a.Equals(b);
    }

    public static bool operator !=(Instrument? a, Instrument? b) => !(a == b);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Uid);
        hashCode.Add(Figi);
        hashCode.Add(Name);
        hashCode.Add(AssetType);
        hashCode.Add(Lot);
        hashCode.Add(Otc);
        hashCode.Add(ForQualInvestor);
        hashCode.Add(ApiTradeAvailable);
        hashCode.Add(First1MinCandleDate);
        hashCode.Add(First1DayCandleDate);
        return hashCode.ToHashCode();
    }
}
