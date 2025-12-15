namespace CatalogService.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("Name")]
    public string Name { get; set; } = null!;

    [BsonElement("Price")]
    public decimal Price { get; set; }

    [BsonElement("Stock")]
    public int Stock { get; set; }
}
