using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddApplicationServices();

// config openTelemetry
builder.Services.AddOpenTelemetry().WithMetrics(
    metrics =>
    {
        metrics.AddHttpClientInstrumentation();
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter("eShop.Basket.API", "1.0.0");
        metrics.AddOtlpExporter(
            options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            }
        );
    });



builder.Services.AddGrpc();

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapDefaultEndpoints();

app.MapGrpcService<BasketService>();

app.Run();