using System.Globalization;
using EFCore.NamingConventions.Internal;
using Microsoft.EntityFrameworkCore;

namespace TradingBot.Data;

public class TradingBotContext : DbContext
{
    public TradingBotContext(DbContextOptions<TradingBotContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Singular table names
        var snakeCase = new SnakeCaseNameRewriter(CultureInfo.InvariantCulture);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            entity.SetTableName(snakeCase.RewriteName(entity.DisplayName()));
    }
}
