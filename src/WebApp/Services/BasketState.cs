using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using eShop.WebAppComponents.Catalog;
using eShop.WebAppComponents.Services;

namespace eShop.WebApp.Services;

public class BasketState(
    BasketService basketService,
    CatalogService catalogService,
    OrderingService orderingService,
    AuthenticationStateProvider authenticationStateProvider) : IBasketState
{
    private static readonly Meter Meter = new Meter("eShop.WebApp.Services.BasketState", "1.0.0");
    private static readonly Counter<int> GetBasketItemsCounter = Meter.CreateCounter<int>("get_basket_items_requests");
    private static readonly Counter<int> AddItemCounter = Meter.CreateCounter<int>("add_item_requests");
    private static readonly Counter<int> SetQuantityCounter = Meter.CreateCounter<int>("set_quantity_requests");
    private static readonly Counter<int> CheckoutCounter = Meter.CreateCounter<int>("checkout_requests");
    private static readonly Histogram<double> RequestDurationHistogram = Meter.CreateHistogram<double>("request_duration", "ms", "Duration of requests in milliseconds");

    private static readonly ActivitySource ActivitySource = new ActivitySource("eShop.WebApp.Services.BasketState");

    private Task<IReadOnlyCollection<BasketItem>>? _cachedBasket;
    private HashSet<BasketStateChangedSubscription> _changeSubscriptions = new();

    public Task DeleteBasketAsync()
        => basketService.DeleteBasketAsync();

    public async Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync()
    {
        GetBasketItemsCounter.Add(1);

        using var activity = ActivitySource.StartActivity("GetBasketItems", ActivityKind.Internal);
        activity?.SetTag("user.authenticated", (await GetUserAsync()).Identity?.IsAuthenticated == true);

        var stopwatch = Stopwatch.StartNew();

        var items = (await GetUserAsync()).Identity?.IsAuthenticated == true
            ? await FetchBasketItemsAsync()
            : Array.Empty<BasketItem>();

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "GetBasketItems"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Basket items retrieved successfully");
        return items;
    }

    public IDisposable NotifyOnChange(EventCallback callback)
    {
        var subscription = new BasketStateChangedSubscription(this, callback);
        _changeSubscriptions.Add(subscription);
        return subscription;
    }

    public async Task AddAsync(CatalogItem item)
    {
        AddItemCounter.Add(1);

        using var activity = ActivitySource.StartActivity("AddItem", ActivityKind.Internal);
        activity?.SetTag("item.id", item.Id);
        activity?.SetTag("item.name", item.Name);

        var stopwatch = Stopwatch.StartNew();

        var items = (await FetchBasketItemsAsync()).Select(i => new BasketQuantity(i.ProductId, i.Quantity)).ToList();
        bool found = false;
        for (var i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing.ProductId == item.Id)
            {
                items[i] = existing with { Quantity = existing.Quantity + 1 };
                found = true;
                break;
            }
        }

        if (!found)
        {
            items.Add(new BasketQuantity(item.Id, 1));
        }

        _cachedBasket = null;
        await basketService.UpdateBasketAsync(items);
        await NotifyChangeSubscribersAsync();

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "AddItem"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Item added to basket successfully");
    }

    public async Task SetQuantityAsync(int productId, int quantity)
    {
        SetQuantityCounter.Add(1);

        using var activity = ActivitySource.StartActivity("SetQuantity", ActivityKind.Internal);
        activity?.SetTag("product.id", productId);
        activity?.SetTag("quantity", quantity);

        var stopwatch = Stopwatch.StartNew();

        var existingItems = (await FetchBasketItemsAsync()).ToList();
        if (existingItems.FirstOrDefault(row => row.ProductId == productId) is { } row)
        {
            if (quantity > 0)
            {
                row.Quantity = quantity;
            }
            else
            {
                existingItems.Remove(row);
            }

            _cachedBasket = null;
            await basketService.UpdateBasketAsync(existingItems.Select(i => new BasketQuantity(i.ProductId, i.Quantity)).ToList());
            await NotifyChangeSubscribersAsync();
        }

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "SetQuantity"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Quantity set successfully");
    }

    public async Task CheckoutAsync(BasketCheckoutInfo checkoutInfo)
    {
        CheckoutCounter.Add(1);

        using var activity = ActivitySource.StartActivity("Checkout", ActivityKind.Internal);
        activity?.SetTag("checkout.requestId", checkoutInfo.RequestId);
        activity?.SetTag("checkout.city", checkoutInfo.City);
        activity?.SetTag("checkout.street", checkoutInfo.Street);
        activity?.SetTag("checkout.state", checkoutInfo.State);
        activity?.SetTag("checkout.country", checkoutInfo.Country);
        activity?.SetTag("checkout.zipCode", checkoutInfo.ZipCode);
        activity?.SetTag("checkout.cardTypeId", checkoutInfo.CardTypeId);

        var stopwatch = Stopwatch.StartNew();

        if (checkoutInfo.RequestId == default)
        {
            checkoutInfo.RequestId = Guid.NewGuid();
        }

        var buyerId = await authenticationStateProvider.GetBuyerIdAsync() ?? throw new InvalidOperationException("User does not have a buyer ID");
        var userName = await authenticationStateProvider.GetUserNameAsync() ?? throw new InvalidOperationException("User does not have a user name");

        // Get details for the items in the basket
        var orderItems = await FetchBasketItemsAsync();

        // Call into Ordering.API to create the order using those details
        var request = new CreateOrderRequest(
            UserId: buyerId,
            UserName: userName,
            City: checkoutInfo.City!,
            Street: checkoutInfo.Street!,
            State: checkoutInfo.State!,
            Country: checkoutInfo.Country!,
            ZipCode: checkoutInfo.ZipCode!,
            CardNumber: "1111222233334444",
            CardHolderName: "TESTUSER",
            CardExpiration: DateTime.UtcNow.AddYears(1),
            CardSecurityNumber: "111",
            CardTypeId: checkoutInfo.CardTypeId,
            Buyer: buyerId,
            Items: orderItems.ToList());
        await orderingService.CreateOrder(request, checkoutInfo.RequestId);
        await DeleteBasketAsync();

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("method", "Checkout"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Checkout completed successfully");
    }

    private Task NotifyChangeSubscribersAsync()
        => Task.WhenAll(_changeSubscriptions.Select(s => s.NotifyAsync()));

    private async Task<ClaimsPrincipal> GetUserAsync()
        => (await authenticationStateProvider.GetAuthenticationStateAsync()).User;

    private Task<IReadOnlyCollection<BasketItem>> FetchBasketItemsAsync()
    {
        return _cachedBasket ??= FetchCoreAsync();

        async Task<IReadOnlyCollection<BasketItem>> FetchCoreAsync()
        {
            var quantities = await basketService.GetBasketAsync();
            if (quantities.Count == 0)
            {
                return Array.Empty<BasketItem>();
            }

            // Get details for the items in the basket
            var basketItems = new List<BasketItem>();
            var productIds = quantities.Select(row => row.ProductId);
            var catalogItems = (await catalogService.GetCatalogItems(productIds)).ToDictionary(k => k.Id, v => v);
            foreach (var item in quantities)
            {
                var catalogItem = catalogItems[item.ProductId];
                var orderItem = new BasketItem
                {
                    Id = Guid.NewGuid().ToString(), // TODO: this value is meaningless, use ProductId instead.
                    ProductId = catalogItem.Id,
                    ProductName = catalogItem.Name,
                    UnitPrice = catalogItem.Price,
                    Quantity = item.Quantity,
                };
                basketItems.Add(orderItem);
            }

            return basketItems;
        }
    }

    private class BasketStateChangedSubscription(BasketState Owner, EventCallback Callback) : IDisposable
    {
        public Task NotifyAsync() => Callback.InvokeAsync();
        public void Dispose() => Owner._changeSubscriptions.Remove(this);
    }
}

public record CreateOrderRequest(
    string UserId,
    string UserName,
    string City,
    string Street,
    string State,
    string Country,
    string ZipCode,
    string CardNumber,
    string CardHolderName,
    DateTime CardExpiration,
    string CardSecurityNumber,
    int CardTypeId,
    string Buyer,
    List<BasketItem> Items);
