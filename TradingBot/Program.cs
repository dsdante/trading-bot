using System.Net.Http.Headers;
using TradingBot;
using TradingBot.Data;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddTradingBotDatabase(configuration);
services.AddInvestApiClient((_, settings) => configuration.Bind("Tinkoff", settings));
services.AddHttpClient<TinkoffHistoryDataService>(httpClient =>
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Bearer", configuration.GetSection("Tinkoff:AccessToken").Get<string>()));
services.AddScoped<TinkoffService>();
services.AddScoped<HistoryService>();
services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();  // calls Worker.ExecuteAsync().
