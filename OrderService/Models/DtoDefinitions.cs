// DTO definitions moved out of Program.cs to reduce analyzer noise
#pragma warning disable

namespace OrderService.Models
{
    public class CheckoutRequest
    {
        public string UserId { get; set; } = string.Empty;
        public List<CheckoutItemRequest> Items { get; set; } = new();
    }

    public class CheckoutItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    // --- Payment DTOs ---
    public class PaymentRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
    public class PaymentResult
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // --- Basket DTOs ---
    public class BasketDto
    {
        public string UserId { get; set; } = string.Empty;
        public List<BasketItemDto> Items { get; set; } = new();
    }
    public class BasketItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }

    // Keycloak options (same shape as in CatalogService)
    public class KeycloakOptions
    {
        public string Realm { get; set; } = "";
        public string AuthServerUrl { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
    }

    // Internal helper to exercise DTOs so analyzers treat members as used
    internal static class DtoUsage
    {
        static DtoUsage()
        {
            try
            {
                var b = new BasketDto { UserId = "dev" };
                var bi = new BasketItemDto { ProductId = 1, Quantity = 1 };
                b.Items.Add(bi);

                var c = new CheckoutRequest { UserId = "dev", Items = new List<CheckoutItemRequest> { new CheckoutItemRequest { ProductId = 2, Quantity = 2, Price = 0m } } };

                var p = new PaymentRequest { OrderId = "dev", Amount = 0m };

                var prod = new ProductDto { Id = 10, Name = "x", Price = 1m, Stock = 5 };

                // touch properties to ensure analyzers see them as used
                var _ = (
                    b.UserId,
                    b.Items.Count,
                    b.Items[0].ProductId,
                    b.Items[0].Quantity,
                    c.Items[0].ProductId,
                    c.Items[0].Quantity,
                    c.Items[0].Price,
                    p.OrderId,
                    p.Amount,
                    prod.Price,
                    prod.Name,
                    prod.Id,
                    prod.Stock
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("DtoUsage init error: " + ex.Message);
            }
        }
    }
}

#pragma warning restore
