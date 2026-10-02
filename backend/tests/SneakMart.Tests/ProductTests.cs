using System.Net;
using Xunit;

namespace SneakMart.Tests;

public class ProductTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_product_with_required_fields_returns_201_as_draft()
    {
        var c = await AdminAsync();
        var cat = await NewCategoryAsync(c);

        var (status, body) = await Api.Post(c, "/api/v1/admin/products",
            new { categoryId = cat, name = "Pegasus", slug = Slug("peg"), brand = "Nike" });

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("draft", body.GetProperty("status").GetString());
        Assert.Empty(body.GetProperty("skus").EnumerateArray());
    }

    [Fact]
    public async Task Create_product_without_name_returns_400()
    {
        var c = await AdminAsync();
        var cat = await NewCategoryAsync(c);

        var (status, body) = await Api.Post(c, "/api/v1/admin/products", new { categoryId = cat, slug = Slug("noname"), brand = "Nike" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("VALIDATION_ERROR", Api.Code(body));
    }

    [Fact]
    public async Task Duplicate_product_slug_returns_409()
    {
        var c = await AdminAsync();
        var cat = await NewCategoryAsync(c);
        var slug = Slug("dupprod");
        await NewProductAsync(c, cat, slug);

        var (status, body) = await Api.Post(c, "/api/v1/admin/products", new { categoryId = cat, name = "Again", slug, brand = "Nike" });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("DUPLICATE_SLUG", Api.Code(body));
    }

    [Fact]
    public async Task Product_without_active_sku_cannot_be_activated()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));

        var (status, body) = await Api.Patch(c, $"/api/v1/admin/products/{product}", new { status = "active" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("PRODUCT_HAS_NO_ACTIVE_SKU", Api.Code(body));
    }

    [Fact]
    public async Task Product_with_active_sku_can_be_activated()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        await NewSkuAsync(c, product);

        var (status, body) = await Api.Patch(c, $"/api/v1/admin/products/{product}", new { status = "active" });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("active", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Deactivating_the_last_active_sku_moves_product_back_to_draft()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var sku = await NewSkuAsync(c, product);
        await Api.Patch(c, $"/api/v1/admin/products/{product}", new { status = "active" });

        await Api.Patch(c, $"/api/v1/admin/skus/{sku}", new { isActive = false });

        var (_, body) = await Api.Get(c, $"/api/v1/admin/products/{product}");
        Assert.Equal("draft", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Admin_can_list_and_read_products()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));

        var (listStatus, list) = await Api.Get(c, "/api/v1/admin/products?page=1&pageSize=100");
        var (getStatus, one) = await Api.Get(c, $"/api/v1/admin/products/{product}");

        Assert.Equal(HttpStatusCode.OK, listStatus);
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
        Assert.Equal(HttpStatusCode.OK, getStatus);
        Assert.Equal(product, one.GetProperty("id").GetInt32());
    }
}
