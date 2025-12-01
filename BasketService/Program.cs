var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory sepet listesi
var baskets = new Dictionary<string, Basket>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sepeti getir
app.MapGet("/api/basket/{userId}", (string userId) =>
{
    if (!baskets.TryGetValue(userId, out var basket))
    {
        basket = new Basket(userId, new List<BasketItem>());
        baskets[userId] = basket;
    }

    return Results.Ok(basket);
});

// Sepete ürün ekle/güncelle
app.MapPost("/api/basket/{userId}", (string userId, BasketItem item) =>
{
    if (!baskets.TryGetValue(userId, out var basket))
    {
        basket = new Basket(userId, new List<BasketItem>());
        baskets[userId] = basket;
    }

    var existing = basket.Items.FirstOrDefault(i => i.ProductId == item.ProductId);

    if (existing is null)
    {
        basket.Items.Add(item);
    }
    else
    {
        basket.Items.Remove(existing);
        basket.Items.Add(existing with { Quantity = existing.Quantity + item.Quantity });
    }

    return Results.Ok(basket);
});

app.Run();


public record BasketItem(int ProductId, int Quantity);
public record Basket(string UserId, List<BasketItem> Items);