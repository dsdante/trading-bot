using Grpc.Core;
using Tinkoff.InvestApi.V1;
using AssetType = TradingBot.Data.AssetType;
using Instrument = TradingBot.Data.Instrument;

namespace TradingBot;

internal static class Extensions
{
    // Two methods missing in Tinkoff API C# library

    private static readonly InstrumentsRequest instrumentsRequest = new();

    public static AsyncUnaryCall<CurrenciesResponse> CurrenciesAsync(
        this InstrumentsService.InstrumentsServiceClient instrumentsServiceClient,
        CancellationToken cancellationToken = default) =>
        instrumentsServiceClient.CurrenciesAsync(instrumentsRequest, null, null, cancellationToken);

    [Obsolete("Marked obsolete in the underlying Tinkoff API C# library")]
    public static AsyncUnaryCall<OptionsResponse> OptionsAsync(
        this InstrumentsService.InstrumentsServiceClient instrumentsServiceClient,
        CancellationToken cancellationToken = default) =>
        instrumentsServiceClient.OptionsAsync(instrumentsRequest, null, null, cancellationToken);


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
