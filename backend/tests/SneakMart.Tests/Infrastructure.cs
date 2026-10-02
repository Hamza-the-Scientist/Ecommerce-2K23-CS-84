using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SneakMart.Api.Data;
using SneakMart.Api.Domain;
using Testcontainers.MySql;
using Xunit;

namespace SneakMart.Tests;

/// <summary>Starts a real MySQL 8 container so that DB constraints are tested on the real engine.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "test-admin@sneakmart.local";
    public const string AdminPassword = "Admin#12345";
    public const string CustomerEmail = "test-customer@sneakmart.local";
    public const string CustomerPassword = "Customer#12345";

    private readonly MySqlContainer _mysql = new MySqlBuilder().WithImage("mysql:8.0").Build();

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();

        // Program.cs reads configuration eagerly, so use environment variables (set before the host is built).
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _mysql.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "test-only-signing-key-0123456789-abcdefghij");

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        db.Users.AddRange(
            new User { FullName = "Test Admin", Email = AdminEmail, Role = Roles.Admin, PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword) },
            new User { FullName = "Test Customer", Email = CustomerEmail, Role = Roles.Customer, PasswordHash = BCrypt.Net.BCrypt.HashPassword(CustomerPassword) });
        await db.SaveChangesAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => _mysql.DisposeAsync().AsTask();

    public async Task<HttpClient> ClientForAsync(string email, string password)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }

public static class Api
{
    public static async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpClient c, HttpMethod method, string url, object? body = null)
    {
        using var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        using var res = await c.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        return (res.StatusCode, doc.RootElement.Clone());
    }

    public static Task<(HttpStatusCode Status, JsonElement Body)> Get(HttpClient c, string url) => SendAsync(c, HttpMethod.Get, url);
    public static Task<(HttpStatusCode Status, JsonElement Body)> Post(HttpClient c, string url, object body) => SendAsync(c, HttpMethod.Post, url, body);
    public static Task<(HttpStatusCode Status, JsonElement Body)> Patch(HttpClient c, string url, object body) => SendAsync(c, HttpMethod.Patch, url, body);

    public static string Code(JsonElement body) => body.GetProperty("code").GetString()!;
    public static int Id(JsonElement body) => body.GetProperty("id").GetInt32();
}

[Collection("api")]
public abstract class ApiTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    protected static string Slug(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    protected Task<HttpClient> AdminAsync() => Factory.ClientForAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

    protected async Task<int> NewCategoryAsync(HttpClient c, int? parentId = null, string? slug = null)
    {
        var (status, body) = await Api.Post(c, "/api/v1/admin/categories", new { name = "Test Category", slug = slug ?? Slug("cat"), parentId });
        Assert.Equal(HttpStatusCode.Created, status);
        return Api.Id(body);
    }

    protected async Task<int> NewProductAsync(HttpClient c, int categoryId, string? slug = null)
    {
        var (status, body) = await Api.Post(c, "/api/v1/admin/products",
            new { categoryId, name = "Test Sneaker", slug = slug ?? Slug("prod"), brand = "Nike" });
        Assert.Equal(HttpStatusCode.Created, status);
        return Api.Id(body);
    }

    protected async Task<int> NewVariantAsync(HttpClient c, int productId, string name = "Black/White")
    {
        var (status, body) = await Api.Post(c, $"/api/v1/admin/products/{productId}/variants", new { name, color = "Black" });
        Assert.Equal(HttpStatusCode.Created, status);
        return Api.Id(body);
    }

    protected Task<(HttpStatusCode Status, JsonElement Body)> NewSkuRawAsync(
        HttpClient c, int productId, string? code = null, int? variantId = null, string size = "US 9", decimal price = 100m, int stock = 5) =>
        Api.Post(c, $"/api/v1/admin/products/{productId}/skus",
            new { skuCode = code ?? Slug("SKU").ToUpperInvariant(), variantId, sizeLabel = size, price, stockQuantity = stock });

    protected async Task<int> NewSkuAsync(HttpClient c, int productId, int? variantId = null, string size = "US 9", int stock = 5)
    {
        var (status, body) = await NewSkuRawAsync(c, productId, null, variantId, size, 100m, stock);
        Assert.Equal(HttpStatusCode.Created, status);
        return Api.Id(body);
    }
}
