using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NUnit.Framework;
using System.Data.Common;
using TradingBot.Data;

namespace TradingBotTests;

[SetUpFixture]
public class Configuration
{
    public static IConfigurationRoot Root { get; } = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddUserSecrets<Configuration>()
        .Build();

    public static ILoggerFactory LoggerFactory { get; } = Microsoft.Extensions.Logging.LoggerFactory
        .Create(builder => builder.AddConfiguration(Root.GetSection("Logging"))
                                  .AddSimpleConsole(config => Root.Bind("Logging:Console", config)));

    public static IOptions<NpgsqlConnectionStringBuilder> ConnectionStringBuilder { get; private set; }

    public static string ConnectionString { get; private set; }

    public static DbDataSource DataSource { get; private set; }

    public static DbContextOptionsBuilder<TradingBotDbContext> DbContextOptionsBuilder { get; private set; }

    public static string TInvestToken { get; private set; }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var connectionStringBuilder = Root.GetSection("Database").Get<NpgsqlConnectionStringBuilder>();
        Assert.That(connectionStringBuilder, Is.Not.Null, "Database is not configured.");
        Assert.That(connectionStringBuilder.Database, Is.Not.WhiteSpace, "Connection string is not set.");
        Assert.That(connectionStringBuilder.Database, Is.Not.EqualTo("trading_bot"), "Do not run tests on the production database.");
        ConnectionStringBuilder = Options.Create(connectionStringBuilder);
        ConnectionString = connectionStringBuilder.ConnectionString;

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
        dataSourceBuilder.MapEnum<AssetType>();
        DataSource = dataSourceBuilder.Build();

        var dbContextLogger = LoggerFactory.CreateLogger<TradingBotDbContext>();
        DbContextOptionsBuilder = new DbContextOptionsBuilder<TradingBotDbContext>()
            .UseNpgsql(DataSource)
            .EnableSensitiveDataLogging();
        using (var dbContext = new TradingBotDbContext(DbContextOptionsBuilder.Options, dbContextLogger))
            await dbContext.Database.EnsureCreatedAsync();

        TInvestToken = Root.GetSection("TInvest:AccessToken").Get<string>()!;
        Assert.That(TInvestToken, Is.Not.WhiteSpace, "T-Invest token is not set.");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (DataSource != null)
            await DataSource.DisposeAsync();
        LoggerFactory?.Dispose();
    }
}
