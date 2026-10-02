using System.Net;
using Xunit;

namespace SneakMart.Tests;

public class AuthorizationTests(ApiFactory factory) : ApiTestBase(factory)
{
    public static IEnumerable<object[]> AdminRoutes =>
    [
        ["GET",   "/api/v1/admin/products"],
        ["POST",  "/api/v1/admin/products"],
        ["PATCH", "/api/v1/admin/products/1"],
        ["POST",  "/api/v1/admin/products/1/variants"],
        ["POST",  "/api/v1/admin/products/1/skus"],
        ["PATCH", "/api/v1/admin/skus/1"],
        ["GET",   "/api/v1/admin/categories"],
        ["POST",  "/api/v1/admin/categories"],
        ["PATCH", "/api/v1/admin/categories/1"],
    ];

    static object? BodyFor(string method) => method == "GET" ? null : new { };

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Request_without_token_returns_401(string method, string url)
    {
        var client = Factory.CreateClient();

        var (status, body) = await Api.SendAsync(client, new HttpMethod(method), url, BodyFor(method));

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal("UNAUTHENTICATED", Api.Code(body));
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Customer_token_returns_403(string method, string url)
    {
        var client = await Factory.ClientForAsync(ApiFactory.CustomerEmail, ApiFactory.CustomerPassword);

        var (status, body) = await Api.SendAsync(client, new HttpMethod(method), url, BodyFor(method));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("FORBIDDEN", Api.Code(body));
    }

    [Fact]
    public async Task Admin_token_is_accepted()
    {
        var client = await AdminAsync();

        var (products, _) = await Api.Get(client, "/api/v1/admin/products");
        var (categories, _) = await Api.Get(client, "/api/v1/admin/categories");

        Assert.Equal(HttpStatusCode.OK, products);
        Assert.Equal(HttpStatusCode.OK, categories);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = Factory.CreateClient();

        var (status, body) = await Api.Post(client, "/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal("UNAUTHENTICATED", Api.Code(body));
    }
}
