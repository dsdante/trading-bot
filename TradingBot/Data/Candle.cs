using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TradingBot.Data;

[PrimaryKey(nameof(InstrumentId), nameof(Timestamp))]
public class Candle
{
    [Column("instrument")]
    public short InstrumentId { get; init; }
    public DateTime Timestamp { get; init; }
    public float Open { get; init; }
    public float High { get; init; }
    public float Low { get; init; }
    public float Close { get; init; }
    public int Volume { get; init; }  // TODO: trim to int.MaxValue

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public required Instrument Instrument { get; init; }
}
