using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using TradingBot.Data;

namespace TradingBotTests;

[SetUpFixture]
public class Configuration
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
            var connectionStringOptions = Root.GetSection("Database").Get<NpgsqlConnectionStringBuilder>();
            Assert.Multiple(() =>
            {
                Assert.That(connectionStringOptions?.Database, Is.Not.Null, "Connection string not set.");
                Assert.That(connectionStringOptions!.Database, Is.Not.EqualTo("trading_bot"), "Do not test on the production database.");
            });
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

    public static TradingBotDbContext DbContext
    {
        get
        {
            if (dbContext != null)
                return dbContext;
            var logger = LoggerFactory.CreateLogger<TradingBotDbContext>();
            var optionsBuilder = new DbContextOptionsBuilder<TradingBotDbContext>().UseNpgsql(DataSource);
            dbContext = new TradingBotDbContext(optionsBuilder.Options, logger);
            return dbContext;
        }
    }

    private static IConfigurationRoot? root;
    private static ILoggerFactory? loggerFactory;
    private static string? connectionString;
    private static DbDataSource? dataSource;
    private static TradingBotDbContext? dbContext;

    [OneTimeSetUp]
    public async Task OneTimeSetUp() =>
        await DbContext.Database.EnsureCreatedAsync();

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        loggerFactory?.Dispose();
        if (dbContext != null)
            await dbContext.DisposeAsync();
        if (dataSource != null)
            await dataSource.DisposeAsync();
    }
}
