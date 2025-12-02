namespace OrderService.Models;

public record Order(
    string OrderId,
    string UserId,
    List<OrderItem> Items,
    decimal TotalPrice,
    string Status,
    DateTime CreatedAt
);
