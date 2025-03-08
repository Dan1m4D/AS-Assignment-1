using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Build a resource configuration action to set service information.
Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: builder.Configuration.GetValue("ServiceName", defaultValue: "otel-test")!,
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName);

// Configure OpenTelemetry tracing & metrics with auto-start using the
// AddOpenTelemetry extension from OpenTelemetry.Extensions.Hosting.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(configureResource)
    .WithTracing(builder =>
    {
        builder
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        // Use IConfiguration binding for AspNetCore instrumentation options.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));

        builder.AddOtlpExporter(otlpOptions =>
        {
            // Use IConfiguration directly for Otlp exporter endpoint option.
            otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
        });
    })
    .WithMetrics(builder =>
    {
        builder
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        builder.AddOtlpExporter(otlpOptions =>
        {
            // Use IConfiguration directly for Otlp exporter endpoint option.
            otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
        });
    });

    // Clear default logging providers used by WebApplication host.
builder.Logging.ClearProviders();

// Configure OpenTelemetry Logging.
builder.Logging.AddOpenTelemetry(options =>
{
    // Note: See appsettings.json Logging:OpenTelemetry section for configuration.

    var resourceBuilder = ResourceBuilder.CreateDefault();
    configureResource(resourceBuilder);
    options.SetResourceBuilder(resourceBuilder);

    options.AddOtlpExporter(otlpOptions =>    using System.Diagnostics.Metrics;
    
    var builder = WebApplication.CreateBuilder(args);
    
    // Create a Meter instance for your application
    var meter = new Meter("Basket.API", "1.0.0");
    
    // Create a counter for tracking the number of items added to the basket
    var itemsAddedCounter = meter.CreateCounter<int>("basket_items_added", "items", "Number of items added to the basket");
    
    // Build a resource configuration action to set service information.
    Action<ResourceBuilder> configureResource = r => r.AddService(
        serviceName: builder.Configuration.GetValue("ServiceName", defaultValue: "otel-test")!,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
        serviceInstanceId: Environment.MachineName);
    
    // Configure OpenTelemetry tracing & metrics with auto-start using the
    // AddOpenTelemetry extension from OpenTelemetry.Extensions.Hosting.
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(configureResource)
        .WithTracing(builder =>
        {
            builder
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();
    
            // Use IConfiguration binding for AspNetCore instrumentation options.
            builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));
    
            builder.AddOtlpExporter(otlpOptions =>
            {
                // Use IConfiguration directly for Otlp exporter endpoint option.
                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
            });
        })
        .WithMetrics(builder =>
        {
            builder
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();
    
            builder.AddOtlpExporter(otlpOptions =>
            {
                // Use IConfiguration directly for Otlp exporter endpoint option.
                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
            });
        });
    
    // Clear default logging providers used by WebApplication host.
    builder.Logging.ClearProviders();
    
    // Configure OpenTelemetry Logging.
    builder.Logging.AddOpenTelemetry(options =>
    {
        // Note: See appsettings.json Logging:OpenTelemetry section for configuration.
    
        var resourceBuilder = ResourceBuilder.CreateDefault();
        configureResource(resourceBuilder);
        options.SetResourceBuilder(resourceBuilder);
    
        options.AddOtlpExporter(otlpOptions =>
        {
            // Use IConfiguration directly for Otlp exporter endpoint option.
            otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
        });
    });
    
    builder.AddBasicServiceDefaults();
    builder.AddApplicationServices();
    
    builder.Services.AddGrpc();
    
    var app = builder.Build();
    
    app.MapDefaultEndpoints();
    
    app.MapGrpcService<BasketService>();
    
    // Example of recording a metric
    app.MapPost("/add-to-basket", (int itemId) =>
    {
        // Increment the counter when an item is added to the basket
        itemsAddedCounter.Add(1);
    
        return Results.Ok();
    });
    
    app.Run();
    {
        // Use IConfiguration directly for Otlp exporter endpoint option.
        otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
    });
});


builder.AddBasicServiceDefaults();
builder.AddApplicationServices();

builder.Services.AddGrpc();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGrpcService<BasketService>();

app.Run();
