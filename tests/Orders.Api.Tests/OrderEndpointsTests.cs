using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Orders.Api.Contracts;
using Orders.Domain;

namespace Orders.Api.Tests;

public sealed class OrderEndpointsTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly OrdersApiFactory _factory = new();
    private readonly HttpClient _client;

    public OrderEndpointsTests()
    {
        _client = _factory.CreateAuthorizedClient();
    }

    private static CreateOrderRequest ValidRequest(string customerId = "customer-1") => new(
        customerId,
        [new OrderItemRequest("KEYCHAIN-01", 2, 15.50m), new OrderItemRequest("PLANTER-02", 1, 89.90m)]);

    private Task<HttpResponseMessage> PostAsync(CreateOrderRequest body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/orders") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Requests_without_api_key_are_rejected()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Repository.Count);
    }

    [Fact]
    public async Task Health_is_public()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_201_stores_pending_order_and_publishes_event()
    {
        var response = await PostAsync(ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(Json);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(120.90m, order.Total);
        Assert.Equal($"/orders/{order.Id}", response.Headers.Location?.OriginalString);

        var published = Assert.Single(_factory.Publisher.Published);
        Assert.Equal(order.Id, published.OrderId);
    }

    [Fact]
    public async Task Invalid_order_returns_400_with_field_errors()
    {
        var response = await PostAsync(new CreateOrderRequest("", [new OrderItemRequest("A", 0, -1m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("customerId", body, StringComparison.Ordinal);
        Assert.Contains("items[0]", body, StringComparison.Ordinal);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_same_order()
    {
        var first = await PostAsync(ValidRequest(), "checkout-abc-123");
        var retry = await PostAsync(ValidRequest(), "checkout-abc-123");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<OrderResponse>(Json);
        var b = await retry.Content.ReadFromJsonAsync<OrderResponse>(Json);
        Assert.Equal(a!.Id, b!.Id);
        Assert.Equal(1, _factory.Repository.Count);
    }

    [Fact]
    public async Task Idempotency_key_is_scoped_per_customer()
    {
        var a = await PostAsync(ValidRequest("customer-1"), "same-key-0001");
        var b = await PostAsync(ValidRequest("customer-2"), "same-key-0001");

        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
        Assert.Equal(2, _factory.Repository.Count);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_is_a_conflict()
    {
        await PostAsync(ValidRequest(), "checkout-abc-123");

        var response = await PostAsync(
            new CreateOrderRequest("customer-1", [new OrderItemRequest("LAMP-04", 1, 199m)]), "checkout-abc-123");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_idempotency_key_is_rejected()
    {
        var response = await PostAsync(ValidRequest(), "bad key!");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Publish_failure_returns_503_and_retry_with_same_key_recovers()
    {
        _factory.Publisher.Fail = true;
        var failed = await PostAsync(ValidRequest(), "checkout-retry-1");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(1, _factory.Repository.Count);
        Assert.Empty(_factory.Publisher.Published);

        _factory.Publisher.Fail = false;
        var retry = await PostAsync(ValidRequest(), "checkout-retry-1");

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Single(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Get_returns_order_or_404()
    {
        var created = await (await PostAsync(ValidRequest())).Content.ReadFromJsonAsync<OrderResponse>(Json);

        var found = await _client.GetAsync(new Uri($"/orders/{created!.Id}", UriKind.Relative));
        var missing = await _client.GetAsync(new Uri("/orders/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task List_returns_only_the_customers_orders()
    {
        await PostAsync(ValidRequest("customer-1"));
        await PostAsync(ValidRequest("customer-1"));
        await PostAsync(ValidRequest("customer-2"));

        var orders = await _client.GetFromJsonAsync<List<OrderResponse>>("/orders?customerId=customer-1", Json);
        var noFilter = await _client.GetAsync(new Uri("/orders", UriKind.Relative));

        Assert.Equal(2, orders!.Count);
        Assert.All(orders, o => Assert.Equal("customer-1", o.CustomerId));
        Assert.Equal(HttpStatusCode.BadRequest, noFilter.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
