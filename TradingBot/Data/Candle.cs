using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Data;

[PrimaryKey(nameof(InstrumentId), nameof(TimestampMinutes))]
public class Candle
{
    [Column("instrument")] public short InstrumentId { get; init; }
    [Column("timestamp")] public int TimestampMinutes { get; init; }
    public float Open { get; init; }
    public float High { get; init; }
    public float Low { get; init; }
    public float Close { get; init; }
    public long Volume { get; init; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public required Instrument Instrument { get; init; }

    [NotMapped]
    public DateTime Timestamp
    {
        get => ToDateTime(TimestampMinutes);
        init => TimestampMinutes = ToMinutes(value);
    }

    public static int ToMinutes(DateTime dateTime, bool round = false)
    {
        if (dateTime.Kind == DateTimeKind.Local)
            throw new InvalidOperationException("Local timestamps are not supported.");
        if (!round && dateTime.Ticks % 0x23C34600L != 0)
            throw new InvalidOperationException("A timestamp must be a whole number of minutes.");
        return (int)((dateTime.Ticks - 0x8C1220247E44000L) / 0x23C34600L);
    }

    public static DateTime ToDateTime(int minutes) => new(0x8C1220247E44000L + minutes * 0x23C34600L, DateTimeKind.Utc);
}
