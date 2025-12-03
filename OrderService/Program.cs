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

    var client = httpClientFactory.CreateClient();

    // get products from basket service
    var basketResponse = await client.GetAsync($"http://localhost:5217/api/basket/{request.UserId}");

    if (!basketResponse.IsSuccessStatusCode)
        return Results.BadRequest("Sepet bulunamadı.");

    var basket = await basketResponse.Content.ReadFromJsonAsync<BasketDto>();

    if (basket is null || basket.Items.Count == 0)
        return Results.BadRequest("Sepet boş.");

    // get price from catalog service

    var orderItems = new List<OrderItem>();

    foreach (var item in basket.Items)
    {
        var catalogResponse = await client.GetAsync($"http://localhost:5278/api/catalog/products/{item.ProductId}");
        if (!catalogResponse.IsSuccessStatusCode)
            return Results.BadRequest($"Ürün bulunamadı: {item.ProductId}");

        var product = await catalogResponse.Content.ReadFromJsonAsync<ProductDto>();
        if (product is null)
            return Results.BadRequest($"Ürün bilgisi alınamadı: {item.ProductId}");

        orderItems.Add(new OrderItem(
            ProductId: item.ProductId,
            Quantity: item.Quantity,
            Price: product.Price));
    }

    var total = orderItems.Sum(i => i.Quantity * i.Price);

    var orderId = Guid.NewGuid().ToString("N");

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

    var paymentResponse = await client.PostAsJsonAsync("http://localhost:5220/api/payment", new PaymentRequest(
        OrderId: orderId,
        Amount: total
    ));
    var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentResult>();

    // If the payment is success
    if (payment?.Status == "Paid")
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

// --- Basket DTOs ---
public record BasketDto(string UserId, List<BasketItemDto> Items);
public record BasketItemDto(int ProductId, int Quantity);

public record ProductDto(int Id, string Name, decimal Price, int Stock);

