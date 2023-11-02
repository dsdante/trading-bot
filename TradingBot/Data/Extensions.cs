using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TradingBot.Data;

internal static class Extensions
{
    public static IServiceCollection AddTradingBotDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredSection("Database")
                                            .Get<NpgsqlConnectionStringBuilder>()
                                            !.ConnectionString;
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<AssetType>();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<TradingBotContext>(builder => builder.UseNpgsql(dataSource));
        return services;
    }
}
