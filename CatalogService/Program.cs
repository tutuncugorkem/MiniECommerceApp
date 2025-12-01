var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/catalog/products", () =>
{
    var products = new[]
    {
        new { Id = 1, Name = "Kulaklık", Price = 500m, Stock = 10 },
        new { Id = 2, Name = "Klavye", Price = 750m, Stock = 5 },
        new { Id = 3, Name = "Mouse", Price = 300m, Stock = 20 }
    };

    return Results.Ok(products);
});

app.MapGet("/api/catalog/products/{id:int}", (int id) =>
{
    var products = new[]
    {
        new { Id = 1, Name = "Kulaklık", Price = 500m, Stock = 10 },
        new { Id = 2, Name = "Klavye", Price = 750m, Stock = 5 },
        new { Id = 3, Name = "Mouse", Price = 300m, Stock = 20 }
    };

    var product = products.FirstOrDefault(p => p.Id == id);
    return product is null ? Results.NotFound() : Results.Ok(product);
});


app.Run();

