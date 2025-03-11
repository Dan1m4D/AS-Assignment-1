using System.Diagnostics;
using System.Net.Http.Json;

namespace eShop.WebApp.Services;

public class OrderingService
{
    private static readonly ActivitySource ActivitySource = new ActivitySource("eShop.WebApp.Services.OrderingService");

    private readonly HttpClient httpClient;
    private readonly string remoteServiceBaseUrl = "/api/Orders/";

    public OrderingService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<OrderRecord[]> GetOrders()
    {
        using var activity = ActivitySource.StartActivity("GetOrders", ActivityKind.Client);
        activity?.SetTag("http.method", HttpMethod.Get.Method);
        activity?.SetTag("http.url", remoteServiceBaseUrl);

        try
        {
            var orders = await httpClient.GetFromJsonAsync<OrderRecord[]>(remoteServiceBaseUrl);
            activity?.SetStatus(ActivityStatusCode.Ok, "Orders retrieved successfully");
            return orders!;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to retrieve orders: {ex.Message}");
            throw;
        }
    }

    public async Task CreateOrder(CreateOrderRequest request, Guid requestId)
    {
        using var activity = ActivitySource.StartActivity("CreateOrder", ActivityKind.Client);
        activity?.SetTag("http.method", HttpMethod.Post.Method);
        activity?.SetTag("http.url", remoteServiceBaseUrl);
        activity?.SetTag("request.requestId", requestId);
        activity?.SetTag("request.body", request);

        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, remoteServiceBaseUrl);
            requestMessage.Headers.Add("x-requestid", requestId.ToString());
            requestMessage.Content = JsonContent.Create(request);

            var response = await httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            activity?.SetStatus(ActivityStatusCode.Ok, "Order created successfully");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to create order: {ex.Message}");
            throw;
        }
    }
}

public record OrderRecord(
    int OrderNumber,
    DateTime Date,
    string Status,
    decimal Total);