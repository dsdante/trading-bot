using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OpenApi.Models;
using TradingBot;
using TradingBot.Data;

if (Environment.UserInteractive)
    Console.Title = typeof(Program).Assembly.GetName().Name!;


var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

services.AddTradingBotDatabase(configuration);

// PascalCaseController -> kebab-case-route
services.AddControllers(mvc => mvc.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

services.AddEndpointsApiExplorer();
services.AddSwaggerGen(swaggerGen =>
{
    swaggerGen.SwaggerDoc("v1", new OpenApiInfo { Title = "Trading bot" });
    // Loading method descriptions from <summary>
    // https://learn.microsoft.com/aspnet/core/tutorials/getting-started-with-swashbuckle#xml-comments
    var xmlDocFilename = Assembly.GetExecutingAssembly().GetName().Name + ".xml";
    swaggerGen.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlDocFilename));
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    configuration.AddUserSecrets<Program>();

    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // "UseHsts isn't recommended in development because the HSTS settings are highly cacheable by browsers."
    // https://learn.microsoft.com/aspnet/core/security/enforcing-ssl#http-strict-transport-security-protocol-hsts
    app.UseHsts();
}

app.UseHttpsRedirection();
// TODO: make a clear error to prevent using unsafe HTTP
// if (!HttpContext.Request.IsHttps) { ...

//app.UseAuthentication();
//app.UseAuthorization();

app.MapControllers();

app.Run();
