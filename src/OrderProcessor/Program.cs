using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

const string serviceName = "eShop.OrderProcessor";
const string serviceVersion = "1.0.0";

builder.AddBasicServiceDefaults();
builder.AddApplicationServices();

// config openTelemetry
builder.Services.AddOpenTelemetry().WithMetrics(
    metrics =>
    {
        metrics.AddHttpClientInstrumentation();
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter(serviceName, serviceVersion);
        metrics.AddOtlpExporter(
            options =>
            {
                options.Endpoint = new Uri("http://localhost:4316");
            }
        );
    })
    .WithTracing(
        (tracing) =>
        {
            tracing.AddSource(serviceName);
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddHttpClientInstrumentation();
            tracing.AddGrpcClientInstrumentation();
            tracing.AddOtlpExporter(
                options =>
                {
                    options.Endpoint = new Uri("http://localhost:4317");
                    options.Protocol = OtlpExportProtocol.Grpc;
                }
            );
        }
    );

builder.Services.AddGrpc();


var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
