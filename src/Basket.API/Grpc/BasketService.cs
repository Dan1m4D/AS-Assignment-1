using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using eShop.Basket.API.Repositories;
using eShop.Basket.API.Extensions;
using eShop.Basket.API.Model;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace eShop.Basket.API.Grpc;

public class BasketService : Basket.BasketBase
{
    private static readonly Meter Meter = new Meter("eShop.Basket.API", "1.0.0");
    private static readonly Counter<int> GetBasketCounter = Meter.CreateCounter<int>("get_basket_requests");
    private static readonly Counter<int> UpdateBasketCounter = Meter.CreateCounter<int>("update_basket_requests");
    private static readonly Counter<int> DeleteBasketCounter = Meter.CreateCounter<int>("delete_basket_requests");
    private static readonly Histogram<double> RequestDurationHistogram = Meter.CreateHistogram<double>("request_duration", "ms", "Duration of requests in milliseconds");

    private static readonly ActivitySource ActivitySource = new ActivitySource("eShop.Basket.API");

    private readonly IBasketRepository repository;
    private readonly ILogger<BasketService> logger;

    public BasketService(IBasketRepository repository, ILogger<BasketService> logger)
    {
        this.repository = repository;
        this.logger = logger;
    }

    [AllowAnonymous]
    public override async Task<CustomerBasketResponse> GetBasket(GetBasketRequest request, ServerCallContext context)
    {
        GetBasketCounter.Add(1);

        using var activity = ActivitySource.StartActivity("GetBasket", ActivityKind.Server);
        activity?.SetTag("user.id", context.GetUserIdentity());
        activity?.SetTag("request.method", context.Method);

        var stopwatch = Stopwatch.StartNew();

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "User ID is null or empty");
            return new();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin GetBasketById call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var data = await repository.GetBasketAsync(userId);

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "GetBasket"));

        if (data is not null)
        {
            activity?.SetStatus(ActivityStatusCode.Ok, "Basket retrieved successfully");
            return MapToCustomerBasketResponse(data);
        }

        activity?.SetStatus(ActivityStatusCode.Error, "Basket not found");
        return new();
    }

    public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
    {
        UpdateBasketCounter.Add(1);

        using var activity = ActivitySource.StartActivity("UpdateBasket", ActivityKind.Server);
        activity?.SetTag("user.id", context.GetUserIdentity());
        activity?.SetTag("request.method", context.Method);

        var stopwatch = Stopwatch.StartNew();

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "User ID is null or empty");
            ThrowNotAuthenticated();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin UpdateBasket call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var customerBasket = MapToCustomerBasket(userId, request);
        var response = await repository.UpdateBasketAsync(customerBasket);

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "UpdateBasket"));

        if (response is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Basket not found");
            ThrowBasketDoesNotExist(userId);
        }

        activity?.SetStatus(ActivityStatusCode.Ok, "Basket updated successfully");
        return MapToCustomerBasketResponse(response);
    }

    public override async Task<DeleteBasketResponse> DeleteBasket(DeleteBasketRequest request, ServerCallContext context)
    {
        DeleteBasketCounter.Add(1);

        using var activity = ActivitySource.StartActivity("DeleteBasket", ActivityKind.Server);
        activity?.SetTag("user.id", context.GetUserIdentity());
        activity?.SetTag("request.method", context.Method);

        var stopwatch = Stopwatch.StartNew();

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "User ID is null or empty");
            ThrowNotAuthenticated();
        }

        await repository.DeleteBasketAsync(userId);

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "DeleteBasket"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Basket deleted successfully");
        return new();
    }

    [DoesNotReturn]
    private static void ThrowNotAuthenticated() => throw new RpcException(new Status(StatusCode.Unauthenticated, "The caller is not authenticated."));

    [DoesNotReturn]
    private static void ThrowBasketDoesNotExist(string userId) => throw new RpcException(new Status(StatusCode.NotFound, $"Basket with buyer id {userId} does not exist"));

    private static CustomerBasketResponse MapToCustomerBasketResponse(CustomerBasket customerBasket)
    {
        var response = new CustomerBasketResponse();

        foreach (var item in customerBasket.Items)
        {
            response.Items.Add(new BasketItem()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }

    private static CustomerBasket MapToCustomerBasket(string userId, UpdateBasketRequest customerBasketRequest)
    {
        var response = new CustomerBasket
        {
            BuyerId = userId
        };

        foreach (var item in customerBasketRequest.Items)
        {
            response.Items.Add(new()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }
}