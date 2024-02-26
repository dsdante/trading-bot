using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using TradingBot.Data;

namespace TradingBotTests;

[SetUpFixture]
public class Configuration : IAsyncDisposable
{
    // TODO: add Worker SDK, ServiceProvider

    public static IConfigurationRoot Root =>
        root ??= new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Configuration>()
            .Build();

    public static ILoggerFactory LoggerFactory =>
        loggerFactory ??= Microsoft.Extensions.Logging.LoggerFactory
            .Create(builder => builder.AddConfiguration(Root.GetSection("Logging"))
                                      .AddSimpleConsole(config => Root.Bind("Logging:Console", config)));

    public static string ConnectionString
    {
        get
        {
            if (connectionString != null)
                return connectionString;
            var connectionStringOptions = Root.GetSection("Database")
                                              .Get<NpgsqlConnectionStringBuilder>();
            Assert.That(connectionStringOptions?.Database, Is.Not.Null, "Connection string not set.");
            Assert.That(connectionStringOptions!.Database, Is.Not.EqualTo("trading_bot"), "Do not test on the production database.");
            return connectionString = connectionStringOptions.ConnectionString;
        }
    }

    public static DbDataSource DataSource
    {
        get
        {
            if (dataSource != null)
                return dataSource;
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
            dataSourceBuilder.MapEnum<AssetType>();
            return dataSource = dataSourceBuilder.Build();
        }
    }

    private static IConfigurationRoot? root;
    private static ILoggerFactory? loggerFactory;
    private static string? connectionString;
    private static DbDataSource? dataSource;

    public async ValueTask DisposeAsync()
    {
        loggerFactory?.Dispose();
        if (dataSource != null)
            await dataSource.DisposeAsync();
    }
}
