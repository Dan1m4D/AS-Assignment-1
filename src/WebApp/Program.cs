using eShop.WebApp.Components;
using eShop.ServiceDefaults;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

const string serviceName = "eShop.WebApp.Services";
const string serviceVersion = "1.0.0";

builder.AddServiceDefaults();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.AddApplicationServices();

// config openTelemetry
builder.Services.AddOpenTelemetry().WithMetrics(
    metrics =>
    {
        metrics.AddHttpClientInstrumentation();
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter(serviceName + ".BasketState", serviceVersion);
        metrics.AddMeter(serviceName + ".BasketService", serviceVersion);
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
            tracing.AddSource(serviceName + ".BasketState");
            tracing.AddSource(serviceName + ".BasketService");
            tracing.AddSource(serviceName + ".OrderingService");
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

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseAntiforgery();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapForwarder("/product-images/{id}", "http://catalog-api", "/api/catalog/items/{id}/pic");

app.Run();
