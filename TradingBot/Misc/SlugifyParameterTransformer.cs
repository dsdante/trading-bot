using System.Text.RegularExpressions;

namespace TradingBot;

// PascalCase -> kebab-case https://learn.microsoft.com/aspnet/core/fundamentals/routing#parameter-transformers
internal partial class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SlugifyPattern();

    public string? TransformOutbound(object? value)
    {
        if (value == null)
            return null;
        return SlugifyPattern().Replace(value.ToString() ?? "", "$1-$2").ToLowerInvariant();
    }
}
