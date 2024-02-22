using TradingBot;
using TradingBot.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTradingBotDatabase(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
