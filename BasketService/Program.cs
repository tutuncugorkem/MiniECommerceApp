using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory sepet listesi -> var baskets = new Dictionary<string, Basket>();
// redis :
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sepeti getir
app.MapGet("/api/basket/{userId}", async (string userId, IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    var data = await db.StringGetAsync($"basket:{userId}");
    if (data.IsNullOrEmpty)
    {
        return Results.Ok(new Basket(userId, new List<BasketItem>()));
    }
    var basket = System.Text.Json.JsonSerializer.Deserialize<Basket>(data!);

    return Results.Ok(basket);
});

// Sepete ürün ekle/güncelle
app.MapPost("/api/basket/{userId}", async (string userId, BasketItem item, IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    var data = await db.StringGetAsync($"basket:{userId}");
    Basket basket;
    if (data.IsNullOrEmpty)
    {
        basket = new Basket(userId, new List<BasketItem>());
    }
    else
    {
        basket = System.Text.Json.JsonSerializer.Deserialize<Basket>(data!);
    }
    basket.Items.Add(item);
    
    var json = System.Text.Json.JsonSerializer.Serialize(basket);
    await db.StringSetAsync($"basket:{userId}", json);

    return Results.Ok(basket);
});

app.Run();


public record BasketItem(int ProductId, int Quantity);
public record Basket(string UserId, List<BasketItem> Items);