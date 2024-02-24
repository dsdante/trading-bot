using System.Globalization;
using EFCore.NamingConventions.Internal;
using Microsoft.EntityFrameworkCore;

namespace TradingBot.Data;

public class TradingBotDbContext(DbContextOptions<TradingBotDbContext> options) : DbContext(options)
{
    public required DbSet<Instrument> Instruments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<AssetType>();

        // Singular table names
        var snakeCase = new SnakeCaseNameRewriter(CultureInfo.InvariantCulture);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            entity.SetTableName(snakeCase.RewriteName(entity.DisplayName()));
    }
}
