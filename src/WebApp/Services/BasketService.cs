using System.Diagnostics;
using System.Diagnostics.Metrics;
using eShop.Basket.API.Grpc;
using GrpcBasketItem = eShop.Basket.API.Grpc.BasketItem;
using GrpcBasketClient = eShop.Basket.API.Grpc.Basket.BasketClient;

namespace eShop.WebApp.Services;

public class BasketService(GrpcBasketClient basketClient)
{
    private static readonly Meter Meter = new Meter("eShop.WebApp.Services.BasketService", "1.0.0");
    private static readonly Counter<int> GetBasketCounter = Meter.CreateCounter<int>("get_basket_requests");
    private static readonly Counter<int> DeleteBasketCounter = Meter.CreateCounter<int>("delete_basket_requests");
    private static readonly Counter<int> UpdateBasketCounter = Meter.CreateCounter<int>("update_basket_requests");
    private static readonly Histogram<double> RequestDurationHistogram = Meter.CreateHistogram<double>("request_duration", "ms", "Duration of requests in milliseconds");

    private static readonly ActivitySource ActivitySource = new ActivitySource("eShop.WebApp.Services.BasketService");

    public async Task<IReadOnlyCollection<BasketQuantity>> GetBasketAsync()
    {
        GetBasketCounter.Add(1);

        using var activity = ActivitySource.StartActivity("GetBasket", ActivityKind.Client);
        activity?.SetTag("grpc.method", "GetBasket");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await basketClient.GetBasketAsync(new());
            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "GetBasket"));

            activity?.SetStatus(ActivityStatusCode.Ok, "Basket retrieved successfully");
            return MapToBasket(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "GetBasket"));

            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to retrieve basket: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteBasketAsync()
    {
        DeleteBasketCounter.Add(1);

        using var activity = ActivitySource.StartActivity("DeleteBasket", ActivityKind.Client);
        activity?.SetTag("grpc.method", "DeleteBasket");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await basketClient.DeleteBasketAsync(new DeleteBasketRequest());
            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "DeleteBasket"));

            activity?.SetStatus(ActivityStatusCode.Ok, "Basket deleted successfully");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "DeleteBasket"));

            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to delete basket: {ex.Message}");
            throw;
        }
    }

    public async Task UpdateBasketAsync(IReadOnlyCollection<BasketQuantity> basket)
    {
        UpdateBasketCounter.Add(1);

        using var activity = ActivitySource.StartActivity("UpdateBasket", ActivityKind.Client);
        activity?.SetTag("grpc.method", "UpdateBasket");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var updatePayload = new UpdateBasketRequest();

            foreach (var item in basket)
            {
                var updateItem = new GrpcBasketItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                };
                updatePayload.Items.Add(updateItem);
            }

            await basketClient.UpdateBasketAsync(updatePayload);
            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "UpdateBasket"));

            activity?.SetStatus(ActivityStatusCode.Ok, "Basket updated successfully");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "UpdateBasket"));

            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to update basket: {ex.Message}");
            throw;
        }
    }

    private static List<BasketQuantity> MapToBasket(CustomerBasketResponse response)
    {
        var result = new List<BasketQuantity>();
        foreach (var item in response.Items)
        {
            result.Add(new BasketQuantity(item.ProductId, item.Quantity));
        }

        return result;
    }
}

public record BasketQuantity(int ProductId, int Quantity);
