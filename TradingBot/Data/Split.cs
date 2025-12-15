using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Data;

// Information on share splits; filled manually.
[PrimaryKey(nameof(InstrumentId), nameof(TimestampMinutes))]
public class Split
{
    [Column("instrument")] public short InstrumentId { get; init; }
    [Column("timestamp")] public int TimestampMinutes { get; init; }
    [Column("split")] public float SplitFactor { get; init; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public required Instrument Instrument { get; init; }

    [NotMapped]
    public DateTime Timestamp
    {
        get => Candle.ToDateTime(TimestampMinutes);
        init => TimestampMinutes = Candle.ToMinutes(value);
    }
}
