using FRC_Service.Application.Extensions;
using FRC_Service.Infrastructure.Extensions;
using FRC_Service.Presentation.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => 
	config.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplication();
builder.Services.AddPresentation();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.ApplyMigrations();

// Seed supported exchanges
await app.SeedExchangesAsync();

app.AddPresentation(app.Environment);

app.UseSerilogRequestLogging(options => {
	options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

app.Run();

public partial class Program { }