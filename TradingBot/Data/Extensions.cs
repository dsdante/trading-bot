using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TradingBot.Data;

public static class Extensions
{
    public static IServiceCollection AddTradingBotDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var connectionStringConfig = configuration.GetRequiredSection("Database");
        services.Configure<NpgsqlConnectionStringBuilder>(connectionStringConfig);  // for dumping CSVs into the database
        var connectionString = connectionStringConfig.Get<NpgsqlConnectionStringBuilder>()!.ConnectionString;

        var sensitiveDataLogging = configuration.GetSection("Database:EnableSensitiveDataLogging").Get<bool>();
        if (sensitiveDataLogging && !hostEnvironment.IsDevelopment())
            throw new InvalidOperationException("Cannot use EnableSensitiveDataLogging in a non-Development environment.");

        return services.AddDbContext<TradingBotDbContext>(builder => builder
            .UseNpgsql(connectionString, o => o.MapEnum<AssetType>())
            .EnableSensitiveDataLogging(sensitiveDataLogging));
    }
}
