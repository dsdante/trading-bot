using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Data;

// Information on share splits; filled manually.
[PrimaryKey(nameof(InstrumentId), nameof(Timestamp))]
public class Split
{
    [Column("instrument")] public short InstrumentId { get; init; }
    public DateTime Timestamp { get; init; }
    [Column("split")] public float SplitFactor { get; init; }


    [DeleteBehavior(DeleteBehavior.Restrict)]
    public required Instrument Instrument { get; init; }
}
