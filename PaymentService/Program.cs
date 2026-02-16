using MiniECommerce.Bus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMassTransitExt(builder.Configuration);


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/payment", (PaymentRequest request) =>
{
    // Sahte ödeme başarılı
    var paymentResult = new PaymentResult(
        OrderId: request.OrderId,
        Status: "Paid",
        Message: "Ödeme başarılı."
    );

    return Results.Ok(paymentResult);
});

app.Run();

public record PaymentRequest(string OrderId, decimal Amount);
public record PaymentResult(string OrderId, string Status, string Message);