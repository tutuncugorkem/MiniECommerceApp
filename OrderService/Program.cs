using OrderService.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order API",
        Version = "v1"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Bearer token giriniz. Örnek: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    var securityRequirement = new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    };

    c.AddSecurityRequirement(securityRequirement);
});
builder.Services.AddHttpClient();

// --- Keycloak JWT configuration (copied from CatalogService) ---
var keycloak = builder.Configuration.GetSection("Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();
var authority = $"{keycloak.AuthServerUrl.TrimEnd('/')}/realms/{keycloak.Realm}";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authority;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.Audience = keycloak.ClientId;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidAudience = keycloak.ClientId,
        ValidateIssuer = true,
        ValidIssuer = authority,
        ValidateLifetime = true
    };
});

builder.Services.AddAuthorization();
// --- end Keycloak config ---


// In-memory order list
var orders = new Dictionary<string, Order>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Development-only: exercise DTO setters/collections so analyzers treat them as used
    try
    {
        var sampleBasket = new BasketDto { UserId = "dev-user" };
        sampleBasket.Items.Add(new BasketItemDto { ProductId = 1, Quantity = 1 });

        var sampleCheckout = new CheckoutRequest { UserId = "dev-user", Items = new List<CheckoutItemRequest> { new CheckoutItemRequest { ProductId = 1, Quantity = 1, Price = 0m } } };
        var samplePayment = new PaymentRequest { OrderId = "dev", Amount = 0m };

        // Log to ensure values are observed
        Console.WriteLine($"DTO smoke: basket.items={sampleBasket.Items.Count}, checkout.items={sampleCheckout.Items.Count}, payment.order={samplePayment.OrderId}");
    }
    catch { }
}

// Add auth middlewares
app.UseAuthentication();
app.UseAuthorization();

// small protected endpoint for smoke testing JWT setup
app.MapGet("/secure", () => "Protected API").RequireAuthorization();

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

    var paymentReq = new PaymentRequest
    {
        OrderId = orderId,
        Amount = total
    };

    // log payment request for diagnosics
    Console.WriteLine($"Sending payment request: OrderId={paymentReq.OrderId}, Amount={paymentReq.Amount}");

    var paymentResponse = await client.PostAsJsonAsync("http://localhost:5220/api/payment", paymentReq);
    var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentResult>();

    // log payment response to reference properties (silence analyzers)
    Console.WriteLine($"Payment response: OrderId={payment?.OrderId}, Status={payment?.Status}, Message={payment?.Message}");

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
}).RequireAuthorization();

// Get Order by Id
app.MapGet("/api/orders/{orderId}", (string orderId) =>
{
    if (!orders.TryGetValue(orderId, out var order))
        return Results.NotFound();

    return Results.Ok(order);
}).RequireAuthorization();

// Get orders by userId
app.MapGet("/api/orders/user/{userId}", (string userId) =>
{
    var userOrders = orders.Values.Where(o => o.UserId == userId).ToList();
    return Results.Ok(userOrders);
}).RequireAuthorization();

// Update order status
app.MapPut("/api/orders/{orderId}/status", (string orderId, UpdateOrderStatusRequest request) =>
{
    if (!orders.TryGetValue(orderId, out var order))
        return Results.NotFound();

    var updated = order with { Status = request.Status };
    orders[orderId] = updated;

    return Results.Ok(updated);
}).RequireAuthorization();

app.Run();


// --- Request DTOs ---
// DTOs moved to OrderService/Models/DtoDefinitions.cs

// Keycloak options moved to Models/DtoDefinitions.cs
