using CatalogService;
using CatalogService.Models;
using CatalogService.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("DatabaseSettings"));

// Register Mongo client
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// Register Mongo collection
builder.Services.AddSingleton(sp =>
{
    var dbSettings = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    var client = sp.GetRequiredService<IMongoClient>();

    var database = client.GetDatabase(dbSettings.DatabaseName);
    return database.GetCollection<Product>(dbSettings.ProductsCollectionName);
});

// Register MongoDbSettings (strongly-typed) and repository
builder.Services.AddSingleton<IProductRepository, ProductRepository>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Catalog API",
        Version = "v1"
    });

    // JWT Bearer scheme
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


// Bind Keycloak configuration
var keycloak = builder.Configuration.GetSection("Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();
var authority = $"{keycloak.AuthServerUrl.TrimEnd('/')}/realms/{keycloak.Realm}";

// Configure JWT Bearer authentication
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

var app = builder.Build();

// --- New: check MongoDB connectivity early and log helpful message ---
try
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var mongoClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();

    // Try pinging the server
    var db = mongoClient.GetDatabase("admin");
    var ping = db.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
    logger.LogInformation("MongoDB ping successful: {Ping}", ping);
}
catch (Exception ex)
{
    // Log but don't crash the app - this helps debugging local vs docker network issues
    var loggerFactory = app.Services.GetService<ILoggerFactory>();
    var logger = loggerFactory?.CreateLogger("Startup") ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger("Startup");
    logger.LogError(ex, "Failed to connect to MongoDB on startup. Check DatabaseSettings: ConnectionString and whether MongoDB is running (localhost vs docker host).");
}
// --- End check ---

// --- New: seed products automatically when collection is empty (useful for docker-compose startup) ---
try
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

    // Wait / retry loop to allow MongoDB to come up (useful in Docker setups)
    const int maxAttempts = 15;
    var seeded = false;

    for (int attempt = 1; attempt <= maxAttempts && !seeded; attempt++)
    {
        try
        {
            var existing = repo.GetAllAsync().GetAwaiter().GetResult();
            if (!existing.Any())
            {
                var seed = new List<Product>
                {
                    new Product { Name = "Apple iPhone 14", Price = 799m, Stock = 10 },
                    new Product { Name = "Samsung Galaxy S23", Price = 699m, Stock = 15 },
                    new Product { Name = "Google Pixel 7", Price = 599m, Stock = 8 }
                };

                foreach (var p in seed)
                {
                    // ensure Id is empty so Mongo will generate one
                    p.Id = string.Empty;
                    repo.CreateAsync(p).GetAwaiter().GetResult();
                }

                logger.LogInformation("Seeded {Count} products into MongoDB.", seed.Count);
            }
            else
            {
                logger.LogInformation("MongoDB already contains {Count} products; skipping seeding.", existing.Count());
            }

            seeded = true; // success
        }
        catch (Exception ex)
        {
            if (attempt == maxAttempts)
            {
                logger.LogError(ex, "Failed to seed MongoDB after {Attempts} attempts.", attempt);
            }
            else
            {
                logger.LogWarning(ex, "Seeding attempt {Attempt} failed; retrying in 1s...", attempt);
                Thread.Sleep(1000);
            }
        }
    }
}
catch (Exception ex)
{
    var loggerFactory = app.Services.GetService<ILoggerFactory>();
    var logger = loggerFactory?.CreateLogger("Startup") ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger("Startup");
    logger.LogError(ex, "Unexpected error while attempting to seed MongoDB.");
}
// --- End seeding ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/secure", () => "Protected API").RequireAuthorization();

app.MapGet("/api/catalog/products", async (IProductRepository repo) =>
{
    var products = await repo.GetAllAsync();
    return Results.Ok(products);
}).RequireAuthorization();

app.Run();

public record KeycloakOptions
{
    public string Realm { get; init; } = "";
    public string AuthServerUrl { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
}
