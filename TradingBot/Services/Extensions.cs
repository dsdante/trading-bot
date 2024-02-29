using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using Tinkoff.InvestApi.V1;
using AssetType = TradingBot.Data.AssetType;
using Instrument = TradingBot.Data.Instrument;

namespace TradingBot;

internal static class Extensions
{
    // Get a strongly-typed HTTP header value.
    public static T Get<T>(this HttpHeaders headers, string name)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "Empty header name.");

        if (!headers.TryGetValues(name, out var values))
            throw new HttpIOException(HttpRequestError.InvalidResponse, $"Missing header {name}. Received headers:\n{headers}");
        var value = values.First();

        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var converter = TypeDescriptor.GetConverter(type);
        Debug.Assert(converter.CanConvertFrom(typeof(string)), $"Cannot parse type {type.Name}.");
        return (T)converter.ConvertFromString(value)!;
    }


    // API to DB
    // TODO: Use AutoMapper?

    public static Instrument ToInstrument(this Bond response) =>
        new()
        {
            Uid = new Guid(response.Uid),
            Figi = response.Figi,
            Name = response.Name,
            AssetType = AssetType.Bond,
            Lot = response.Lot,
            Otc = response.OtcFlag,
            ForQualInvestor = response.ForQualInvestorFlag,
            ApiTradeAvailable = response.ApiTradeAvailableFlag,
            First1MinCandleDate = response.First1MinCandleDate?.ToDateTime(),
            First1DayCandleDate = response.First1DayCandleDate?.ToDateTime(),
        };

    public static Instrument ToInstrument(this Currency response) =>
        new()
        {
            Uid = new Guid(response.Uid),
            Figi = response.Figi,
            Name = response.Name,
            AssetType = AssetType.Currency,
            Lot = response.Lot,
            Otc = response.OtcFlag,
            ForQualInvestor = response.ForQualInvestorFlag,
            ApiTradeAvailable = response.ApiTradeAvailableFlag,
            First1MinCandleDate = response.First1MinCandleDate?.ToDateTime(),
            First1DayCandleDate = response.First1DayCandleDate?.ToDateTime(),
        };

    public static Instrument ToInstrument(this Etf response) =>
        new()
        {
            Uid = new Guid(response.Uid),
            Figi = response.Figi,
            Name = response.Name,
            AssetType = AssetType.Etf,
            Lot = response.Lot,
            Otc = response.OtcFlag,
            ForQualInvestor = response.ForQualInvestorFlag,
            ApiTradeAvailable = response.ApiTradeAvailableFlag,
            First1MinCandleDate = response.First1MinCandleDate?.ToDateTime(),
            First1DayCandleDate = response.First1DayCandleDate?.ToDateTime(),
        };

    public static Instrument ToInstrument(this Future response) =>
        new()
        {
            Uid = new Guid(response.Uid),
            Figi = response.Figi,
            Name = response.Name,
            AssetType = AssetType.Future,
            Lot = response.Lot,
            Otc = response.OtcFlag,
            ForQualInvestor = response.ForQualInvestorFlag,
            ApiTradeAvailable = response.ApiTradeAvailableFlag,
            First1MinCandleDate = response.First1MinCandleDate?.ToDateTime(),
            First1DayCandleDate = response.First1DayCandleDate?.ToDateTime(),
        };

    public static Instrument ToInstrument(this Option response) =>
        new()
        {
            Uid = new Guid(response.Uid),
            Figi = null,
            Name = response.Name,
            AssetType = AssetType.Option,
            Lot = response.Lot,
            Otc = response.OtcFlag,
            ForQualInvestor = response.ForQualInvestorFlag,
            ApiTradeAvailable = response.ApiTradeAvailableFlag,
            First1MinCandleDate = response.First1MinCandleDate?.ToDateTime(),
            First1DayCandleDate = response.First1DayCandleDate?.ToDateTime(),
        };

    public static Instrument ToInstrument(this Share response) =>
        new()
        {
            Uid = new Guid(response.Uid),
            Figi = response.Figi,
            Name = response.Name,
            AssetType = AssetType.Share,
            Lot = response.Lot,
            Otc = response.OtcFlag,
            ForQualInvestor = response.ForQualInvestorFlag,
            ApiTradeAvailable = response.ApiTradeAvailableFlag,
            First1MinCandleDate = response.First1MinCandleDate?.ToDateTime(),
            First1DayCandleDate = response.First1DayCandleDate?.ToDateTime(),
        };
}
