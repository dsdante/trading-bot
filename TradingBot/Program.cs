using Tinkoff.InvestApi;
using TradingBot;
using TradingBot.Data;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddTradingBotDatabase(configuration);
services.AddInvestApiClient((_, settings) => configuration.Bind("Tinkoff", settings));
services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
