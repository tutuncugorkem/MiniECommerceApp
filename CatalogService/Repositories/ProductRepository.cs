using CatalogService.Models;
using MongoDB.Driver;
using Microsoft.Extensions.Options;

namespace CatalogService.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IMongoCollection<Product> _collection;

    public ProductRepository(IOptions<DatabaseSettings> settings, IMongoClient client)
    {
        var dbSettings = settings.Value;
        var database = client.GetDatabase(dbSettings.DatabaseName);
        _collection = database.GetCollection<Product>(dbSettings.ProductsCollectionName);
    }

    public async Task<IEnumerable<Product>> GetAllAsync() =>
        await _collection.Find(_ => true).ToListAsync();

    public async Task<Product?> GetByIdAsync(string id) =>
        await _collection.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task CreateAsync(Product product) =>
        await _collection.InsertOneAsync(product);

    public async Task UpdateAsync(string id, Product product) =>
        await _collection.ReplaceOneAsync(p => p.Id == id, product);

    public async Task DeleteAsync(string id) =>
        await _collection.DeleteOneAsync(p => p.Id == id);
}