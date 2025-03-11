using System.Diagnostics;
using System.Diagnostics.Metrics;
using eShop.EventBus.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using eShop.OrderProcessor.Events;

namespace eShop.OrderProcessor.Services
{
    public class GracePeriodManagerService(
        IOptions<BackgroundTaskOptions> options,
        IEventBus eventBus,
        ILogger<GracePeriodManagerService> logger,
        NpgsqlDataSource dataSource) : BackgroundService
    {
        private static readonly Meter Meter = new Meter("eShop.OrderProcessor.Services.GracePeriodManagerService", "1.0.0");
        private static readonly Counter<int> CheckOrdersCounter = Meter.CreateCounter<int>("check_orders_requests");
        private static readonly Histogram<double> RequestDurationHistogram = Meter.CreateHistogram<double>("request_duration", "ms", "Duration of requests in milliseconds");

        private static readonly ActivitySource ActivitySource = new ActivitySource("eShop.OrderProcessor.Services.GracePeriodManagerService");

        private readonly BackgroundTaskOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delayTime = TimeSpan.FromSeconds(_options.CheckUpdateTime);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("GracePeriodManagerService is starting.");
                stoppingToken.Register(() => logger.LogDebug("GracePeriodManagerService background task is stopping."));
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("GracePeriodManagerService background task is doing background work.");
                }

                await CheckConfirmedGracePeriodOrders();

                await Task.Delay(delayTime, stoppingToken);
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("GracePeriodManagerService background task is stopping.");
            }
        }

        private async Task CheckConfirmedGracePeriodOrders()
        {
            CheckOrdersCounter.Add(1);

            using var activity = ActivitySource.StartActivity("CheckConfirmedGracePeriodOrders", ActivityKind.Internal);
            activity?.SetTag("method", "CheckConfirmedGracePeriodOrders");

            var stopwatch = Stopwatch.StartNew();

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Checking confirmed grace period orders");
            }

            var orderIds = await GetConfirmedGracePeriodOrders();

            foreach (var orderId in orderIds)
            {
                var confirmGracePeriodEvent = new GracePeriodConfirmedIntegrationEvent(orderId);

                logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", confirmGracePeriodEvent.Id, confirmGracePeriodEvent);

                await eventBus.PublishAsync(confirmGracePeriodEvent);
            }

            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "CheckConfirmedGracePeriodOrders"));

            activity?.SetStatus(ActivityStatusCode.Ok, "Grace period orders checked successfully");
        }

        private async ValueTask<List<int>> GetConfirmedGracePeriodOrders()
        {
            using var activity = ActivitySource.StartActivity("GetConfirmedGracePeriodOrders", ActivityKind.Internal);
            activity?.SetTag("method", "GetConfirmedGracePeriodOrders");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var conn = dataSource.CreateConnection();
                using var command = conn.CreateCommand();
                command.CommandText = """
                    SELECT "Id"
                    FROM ordering.orders
                    WHERE CURRENT_TIMESTAMP - "OrderDate" >= @GracePeriodTime AND "OrderStatus" = 'Submitted'
                    """;
                command.Parameters.AddWithValue("GracePeriodTime", TimeSpan.FromMinutes(_options.GracePeriodTime));

                List<int> ids = new();

                await conn.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    ids.Add(reader.GetInt32(0));
                }

                stopwatch.Stop();
                RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "GetConfirmedGracePeriodOrders"));

                activity?.SetStatus(ActivityStatusCode.Ok, "Confirmed grace period orders retrieved successfully");
                return ids;
            }
            catch (NpgsqlException exception)
            {
                stopwatch.Stop();
                RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "GetConfirmedGracePeriodOrders"));

                activity?.SetStatus(ActivityStatusCode.Error, $"Failed to retrieve confirmed grace period orders: {exception.Message}");
                logger.LogError(exception, "Fatal error establishing database connection");
                return new List<int>();
            }
        }
    }
}
