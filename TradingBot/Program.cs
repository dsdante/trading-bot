using TradingBot;
using TradingBot.Data;
using TradingBot.Services;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddTradingBotDatabase(configuration);
services.AddInvestApiClient((_, settings) => configuration.Bind("Tinkoff", settings));
services.AddSingleton<TinkoffService>();
services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
