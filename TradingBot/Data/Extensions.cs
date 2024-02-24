using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TradingBot.Data;

public static class Extensions
{
    public static IServiceCollection AddTradingBotDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredSection("Database")
                                            .Get<NpgsqlConnectionStringBuilder>()
                                            !.ConnectionString;
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<AssetType>();
        var dataSource = dataSourceBuilder.Build();

        return services.AddDbContext<TradingBotDbContext>(builder => builder.UseNpgsql(dataSource));
    }

    /// <summary> Add new instruments and update those with existing UIDs </summary>
    internal static async Task UpsertRangeAsync(
        this DbSet<Instrument> instrumentDbSet,
        IEnumerable<Instrument> instruments,
        CancellationToken cancellationToken)
    {
        var oldInstruments = await instrumentDbSet.ToDictionaryAsync(i => i.Uid, i => i, cancellationToken);

        foreach (var instrument in instruments)
        {
            if (oldInstruments.TryGetValue(instrument.Uid, out var oldInstrument))
            {
                if (oldInstrument == instrument)
                    continue;
                oldInstrument.Figi = instrument.Figi;
                oldInstrument.Name = instrument.Name;
                oldInstrument.AssetType = instrument.AssetType;
                oldInstrument.Lot = instrument.Lot;
                oldInstrument.Otc = instrument.Otc;
                oldInstrument.ForQualInvestor = instrument.ForQualInvestor;
                oldInstrument.ApiTradeAvailable = instrument.ApiTradeAvailable;
                oldInstrument.First1MinCandleDate = instrument.First1MinCandleDate;
                oldInstrument.First1DayCandleDate = instrument.First1DayCandleDate;
                instrumentDbSet.Update(oldInstrument);
            }
            else
            {
                Debug.Assert(instrument.Id == 0);
                var newEntry = await instrumentDbSet.AddAsync(instrument, cancellationToken);
                oldInstruments.Add(instrument.Uid, newEntry.Entity);
            }
        }
    }
}
