using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http.HttpResults;
using CardType = eShop.Ordering.API.Application.Queries.CardType;
using Order = eShop.Ordering.API.Application.Queries.Order;

public static class OrdersApi
{
    private static readonly Meter Meter = new Meter("eShop.Ordering.API", "1.0.0");
    private static readonly Counter<int> CancelOrderCounter = Meter.CreateCounter<int>("cancel_order_requests");
    private static readonly Counter<int> ShipOrderCounter = Meter.CreateCounter<int>("ship_order_requests");
    private static readonly Counter<int> GetOrderCounter = Meter.CreateCounter<int>("get_order_requests");
    private static readonly Counter<int> GetOrdersByUserCounter = Meter.CreateCounter<int>("get_orders_by_user_requests");
    private static readonly Counter<int> GetCardTypesCounter = Meter.CreateCounter<int>("get_card_types_requests");
    private static readonly Counter<int> CreateOrderDraftCounter = Meter.CreateCounter<int>("create_order_draft_requests");
    private static readonly Counter<int> CreateOrderCounter = Meter.CreateCounter<int>("create_order_requests");
    private static readonly Histogram<double> RequestDurationHistogram = Meter.CreateHistogram<double>("request_duration", "ms", "Duration of requests in milliseconds");

    private static readonly ActivitySource ActivitySource = new ActivitySource("eShop.Ordering.API");

    public static RouteGroupBuilder MapOrdersApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/orders").HasApiVersion(1.0);

        api.MapPut("/cancel", CancelOrderAsync);
        api.MapPut("/ship", ShipOrderAsync);
        api.MapGet("{orderId:int}", GetOrderAsync);
        api.MapGet("/", GetOrdersByUserAsync);
        api.MapGet("/cardtypes", GetCardTypesAsync);
        api.MapPost("/draft", CreateOrderDraftAsync);
        api.MapPost("/", CreateOrderAsync);

        return api;
    }

    public static async Task<Results<Ok, BadRequest<string>, ProblemHttpResult>> CancelOrderAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        CancelOrderCommand command,
        [AsParameters] OrderServices services)
    {
        CancelOrderCounter.Add(1);

        using var activity = ActivitySource.StartActivity("CancelOrder", ActivityKind.Server);
        activity?.SetTag("request.requestId", requestId);
        activity?.SetTag("request.command.orderNumber", command.OrderNumber);

        var stopwatch = Stopwatch.StartNew();

        if (requestId == Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Empty GUID is not valid for request ID");
            return TypedResults.BadRequest("Empty GUID is not valid for request ID");
        }

        var requestCancelOrder = new IdentifiedCommand<CancelOrderCommand, bool>(command, requestId);

        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
            requestCancelOrder.GetGenericTypeName(),
            nameof(requestCancelOrder.Command.OrderNumber),
            requestCancelOrder.Command.OrderNumber,
            requestCancelOrder);

        var commandResult = await services.Mediator.Send(requestCancelOrder);

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "CancelOrder"));

        if (!commandResult)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Cancel order failed to process");
            return TypedResults.Problem(detail: "Cancel order failed to process.", statusCode: 500);
        }

        activity?.SetStatus(ActivityStatusCode.Ok, "Order cancelled successfully");
        return TypedResults.Ok();
    }

    public static async Task<Results<Ok, BadRequest<string>, ProblemHttpResult>> ShipOrderAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        ShipOrderCommand command,
        [AsParameters] OrderServices services)
    {
        ShipOrderCounter.Add(1);

        using var activity = ActivitySource.StartActivity("ShipOrder", ActivityKind.Server);
        activity?.SetTag("request.requestId", requestId);
        activity?.SetTag("request.command.orderNumber", command.OrderNumber);

        var stopwatch = Stopwatch.StartNew();

        if (requestId == Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Empty GUID is not valid for request ID");
            return TypedResults.BadRequest("Empty GUID is not valid for request ID");
        }

        var requestShipOrder = new IdentifiedCommand<ShipOrderCommand, bool>(command, requestId);

        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
            requestShipOrder.GetGenericTypeName(),
            nameof(requestShipOrder.Command.OrderNumber),
            requestShipOrder.Command.OrderNumber,
            requestShipOrder);

        var commandResult = await services.Mediator.Send(requestShipOrder);

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "ShipOrder"));

        if (!commandResult)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Ship order failed to process");
            return TypedResults.Problem(detail: "Ship order failed to process.", statusCode: 500);
        }

        activity?.SetStatus(ActivityStatusCode.Ok, "Order shipped successfully");
        return TypedResults.Ok();
    }

    public static async Task<Results<Ok<Order>, NotFound>> GetOrderAsync(int orderId, [AsParameters] OrderServices services)
    {
        GetOrderCounter.Add(1);

        using var activity = ActivitySource.StartActivity("GetOrder", ActivityKind.Server);
        activity?.SetTag("request.orderId", orderId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var order = await services.Queries.GetOrderAsync(orderId);

            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "GetOrder"));

            activity?.SetStatus(ActivityStatusCode.Ok, "Order retrieved successfully");
            return TypedResults.Ok(order);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Order not found");
            return TypedResults.NotFound();
        }
    }

    public static async Task<Ok<IEnumerable<OrderSummary>>> GetOrdersByUserAsync([AsParameters] OrderServices services)
    {
        GetOrdersByUserCounter.Add(1);

        using var activity = ActivitySource.StartActivity("GetOrdersByUser", ActivityKind.Server);

        var stopwatch = Stopwatch.StartNew();

        var userId = services.IdentityService.GetUserIdentity();
        var orders = await services.Queries.GetOrdersFromUserAsync(userId);

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "GetOrdersByUser"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Orders retrieved successfully");
        return TypedResults.Ok(orders);
    }

    public static async Task<Ok<IEnumerable<CardType>>> GetCardTypesAsync(IOrderQueries orderQueries)
    {
        GetCardTypesCounter.Add(1);

        using var activity = ActivitySource.StartActivity("GetCardTypes", ActivityKind.Server);

        var stopwatch = Stopwatch.StartNew();

        var cardTypes = await orderQueries.GetCardTypesAsync();

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "GetCardTypes"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Card types retrieved successfully");
        return TypedResults.Ok(cardTypes);
    }

    public static async Task<OrderDraftDTO> CreateOrderDraftAsync(CreateOrderDraftCommand command, [AsParameters] OrderServices services)
    {
        CreateOrderDraftCounter.Add(1);

        using var activity = ActivitySource.StartActivity("CreateOrderDraft", ActivityKind.Server);
        activity?.SetTag("request.command.buyerId", command.BuyerId);
        activity?.SetTag("request.command.items", command.Items);

        var stopwatch = Stopwatch.StartNew();

        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
            command.GetGenericTypeName(),
            nameof(command.BuyerId),
            command.BuyerId,
            command);

        var result = await services.Mediator.Send(command);

        stopwatch.Stop();
        RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "CreateOrderDraft"));

        activity?.SetStatus(ActivityStatusCode.Ok, "Order draft created successfully");
        return result;
    }

    public static async Task<Results<Ok, BadRequest<string>>> CreateOrderAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        CreateOrderRequest request,
        [AsParameters] OrderServices services)
    {
        CreateOrderCounter.Add(1);

        using var activity = ActivitySource.StartActivity("CreateOrder", ActivityKind.Server);
        activity?.SetTag("request.requestId", requestId);
        activity?.SetTag("request.command.userId", request.UserId);
        activity?.SetTag("request.command.userName", request.UserName);
        activity?.SetTag("request.command.city", request.City);
        activity?.SetTag("request.command.street", request.Street);
        activity?.SetTag("request.command.state", request.State);
        activity?.SetTag("request.command.country", request.Country);
        activity?.SetTag("request.command.zipCode", request.ZipCode);
        activity?.SetTag("request.command.cardNumber", request.CardNumber);
        activity?.SetTag("request.command.cardHolderName", request.CardHolderName);
        activity?.SetTag("request.command.cardExpiration", request.CardExpiration);
        activity?.SetTag("request.command.cardSecurityNumber", request.CardSecurityNumber);
        activity?.SetTag("request.command.cardTypeId", request.CardTypeId);
        activity?.SetTag("request.command.buyer", request.Buyer);
        activity?.SetTag("request.command.items", request.Items);

        var stopwatch = Stopwatch.StartNew();

        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId}",
            request.GetGenericTypeName(),
            nameof(request.UserId),
            request.UserId); //don't log the request as it has CC number

        if (requestId == Guid.Empty)
        {
            services.Logger.LogWarning("Invalid IntegrationEvent - RequestId is missing - {@IntegrationEvent}", request);
            activity?.SetStatus(ActivityStatusCode.Error, "RequestId is missing");
            return TypedResults.BadRequest("RequestId is missing.");
        }

        using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
        {
            var maskedCCNumber = request.CardNumber.Substring(request.CardNumber.Length - 4).PadLeft(request.CardNumber.Length, 'X');
            var createOrderCommand = new CreateOrderCommand(request.Items, request.UserId, request.UserName, request.City, request.Street,
                request.State, request.Country, request.ZipCode,
                maskedCCNumber, request.CardHolderName, request.CardExpiration,
                request.CardSecurityNumber, request.CardTypeId);

            var requestCreateOrder = new IdentifiedCommand<CreateOrderCommand, bool>(createOrderCommand, requestId);

            services.Logger.LogInformation(
                "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                requestCreateOrder.GetGenericTypeName(),
                nameof(requestCreateOrder.Id),
                requestCreateOrder.Id,
                requestCreateOrder);

            var result = await services.Mediator.Send(requestCreateOrder);

            stopwatch.Stop();
            RequestDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object>("method", "CreateOrder"));

            if (result)
            {
                services.Logger.LogInformation("CreateOrderCommand succeeded - RequestId: {RequestId}", requestId);
                activity?.SetStatus(ActivityStatusCode.Ok, "Order created successfully");
            }
            else
            {
                services.Logger.LogWarning("CreateOrderCommand failed - RequestId: {RequestId}", requestId);
                activity?.SetStatus(ActivityStatusCode.Error, "Order creation failed");
            }

            return TypedResults.Ok();
        }
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
