using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Catalog API",
        Version = "v1"
    });

    // JWT Bearer şeması
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/secure", () => "Protected API").RequireAuthorization();

app.MapGet("/api/catalog/products", () =>
{
    var products = new[]
    {
        new { Id = 1, Name = "Kulaklık", Price = 500m, Stock = 10 },
        new { Id = 2, Name = "Klavye", Price = 750m, Stock = 5 },
        new { Id = 3, Name = "Mouse", Price = 300m, Stock = 20 }
    };

    return Results.Ok(products);
}).RequireAuthorization();

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
}).RequireAuthorization();


app.Run();

public record KeycloakOptions
{
    public string Realm { get; init; } = "";
    public string AuthServerUrl { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
}
