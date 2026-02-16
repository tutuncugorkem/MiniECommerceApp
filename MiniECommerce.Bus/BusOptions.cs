namespace MiniECommerce.Bus;

public class BusOptions
{
    public required string Address { get; set; }

    public required string UserName { get; set; }
    
    public required string Password { get; set; }

    public required int Port { get; set; }
}