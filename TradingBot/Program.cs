using TradingBot;
using TradingBot.Data;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.Configure<TradingBotOptions>(configuration);
services.AddTradingBotDatabase(configuration, builder.Environment);
services.AddInvestApiClient((_, settings) => configuration.Bind("TInvest", settings));
services.AddHttpClient<ITInvestHistoryDataService, TInvestHistoryDataService>(httpClient =>
    httpClient.DefaultRequestHeaders.Authorization =
        new("Bearer", configuration.GetSection("TInvest:AccessToken").Get<string>()));
services.AddScoped<ITInvestService, TInvestService>();
services.AddScoped<HistoryService>();
services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();  // runs Worker.RunAsync()
