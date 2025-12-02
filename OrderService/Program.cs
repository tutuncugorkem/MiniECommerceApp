using OrderService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();


// In-memory order list
var orders = new Dictionary<string, Order>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Create Order (Checkout)
app.MapPost("/api/orders/checkout", async (CheckoutRequest request, IHttpClientFactory httpClientFactory) =>
{
    if (request.Items is null || request.Items.Count == 0)
        return Results.BadRequest("Items boş olamaz");

    var orderId = Guid.NewGuid().ToString("N");

    var orderItems = request.Items
        .Select(i => new OrderItem(i.ProductId, i.Quantity, i.Price))
        .ToList();

    var total = request.Items.Sum(i => i.Quantity * i.Price);

    var order = new Order(
        OrderId: orderId,
        UserId: request.UserId,
        Items: orderItems,
        TotalPrice: total,
        Status: "Created",
        CreatedAt: DateTime.UtcNow
    );

    orders[orderId] = order;

    // Payment request send to payment service
    var client = httpClientFactory.CreateClient();
    var paymentRequest = new PaymentRequest(orderId, total);
    var response = await client.PostAsJsonAsync("https://localhost:7263/api/payment", paymentRequest);
    var paymentResult = await response.Content.ReadFromJsonAsync<PaymentResult>();

    // If the payment is success
    if (paymentResult?.Status == "Paid")
    {
        var updatedOrder = order with { Status = "Paid" };
        orders[orderId] = updatedOrder;

        return Results.Ok(updatedOrder);
    }
    
    var failedOrder = order with { Status = "PaymentFailed" };
    orders[orderId] = failedOrder;
    
    return Results.Ok(failedOrder);
});

// Get Order by Id
app.MapGet("/api/orders/{orderId}", (string orderId) =>
{
    if (!orders.TryGetValue(orderId, out var order))
        return Results.NotFound();

    return Results.Ok(order);
});

// Get orders by userId
app.MapGet("/api/orders/user/{userId}", (string userId) =>
{
    var userOrders = orders.Values.Where(o => o.UserId == userId).ToList();
    return Results.Ok(userOrders);
});

// Update order status
app.MapPut("/api/orders/{orderId}/status", (string orderId, UpdateOrderStatusRequest request) =>
{
    if (!orders.TryGetValue(orderId, out var order))
        return Results.NotFound();

    var updated = order with { Status = request.Status };
    orders[orderId] = updated;

    return Results.Ok(updated);
});

app.Run();


// --- Request DTOs ---
public record CheckoutRequest(string UserId, List<CheckoutItemRequest> Items);
public record CheckoutItemRequest(int ProductId, int Quantity, decimal Price);
public record UpdateOrderStatusRequest(string Status);

// --- Payment DTOs ---
public record PaymentRequest(string OrderId, decimal Amount);
public record PaymentResult(string OrderId, string Status, string Message);