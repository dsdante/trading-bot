using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TradingBot.Data;

public static class Extensions
{
    public static IServiceCollection AddTradingBotDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionStringConfig = configuration.GetRequiredSection("Database");
        services.Configure<NpgsqlConnectionStringBuilder>(connectionStringConfig);  // for dumping CSVs into the database

        var connectionString = connectionStringConfig.Get<NpgsqlConnectionStringBuilder>()!.ConnectionString;
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<AssetType>();
        var dataSource = dataSourceBuilder.Build();
        return services.AddDbContext<TradingBotDbContext>(builder => builder.UseNpgsql(dataSource));
    }
}
