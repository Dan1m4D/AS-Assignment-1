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

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            return new();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin GetBasketById call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var data = await repository.GetBasketAsync(userId);

        if (data is not null)
        {
            return MapToCustomerBasketResponse(data);
        }

        return new();
    }

    public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
    {
        UpdateBasketCounter.Add(1);

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            ThrowNotAuthenticated();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin UpdateBasket call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var customerBasket = MapToCustomerBasket(userId, request);
        var response = await repository.UpdateBasketAsync(customerBasket);
        if (response is null)
        {
            ThrowBasketDoesNotExist(userId);
        }

        return MapToCustomerBasketResponse(response);
    }

    public override async Task<DeleteBasketResponse> DeleteBasket(DeleteBasketRequest request, ServerCallContext context)
    {
        DeleteBasketCounter.Add(1);

        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            ThrowNotAuthenticated();
        }

        await repository.DeleteBasketAsync(userId);
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