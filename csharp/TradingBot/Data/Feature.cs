using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Data;

// A snapshot of engineered features (suitable for machine learning) for a specific instrument and a timestamp
[PrimaryKey(nameof(TimestampMinutes), nameof(InstrumentId))]
public class Feature
{
    [Column("timestamp")] public int TimestampMinutes { get; init; }
    [Column("instrument")] public short InstrumentId { get; init; }

    // All the features are normalized for mean = 0 and standard deviation = 1.
    public float Lag { get; init; }  // ln(timestamp[i] - timestamp[i-1])
    public float Gap { get; init; }  // ln(close[i] / close[i-1])
    public float Volume { get; init; }  // ln(volume * lot * sqrt(low * high) + 1)

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public required Instrument Instrument { get; init; }

    [NotMapped]
    public DateTime Timestamp
    {
        get => Candle.ToDateTime(TimestampMinutes);
        init => TimestampMinutes = Candle.ToMinutes(value);
    }
}
