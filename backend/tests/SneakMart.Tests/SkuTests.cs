using System.Net;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SneakMart.Tests;

public class SkuTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_sku_with_required_fields_returns_201()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var variant = await NewVariantAsync(c, product);

        var (status, body) = await NewSkuRawAsync(c, product, "PEG-OK-9", variant, "US 9", 129.99m, 10);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(129.99m, body.GetProperty("price").GetDecimal());
        Assert.Equal(10, body.GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Create_sku_without_price_returns_400()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));

        var (status, body) = await Api.Post(c, $"/api/v1/admin/products/{product}/skus",
            new { skuCode = Slug("NOPRICE"), sizeLabel = "US 9", stockQuantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("VALIDATION_ERROR", Api.Code(body));
    }

    [Fact]
    public async Task Negative_price_returns_422()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));

        var (status, body) = await NewSkuRawAsync(c, product, price: -1m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("VALIDATION_ERROR", Api.Code(body));
    }

    [Fact]
    public async Task Price_with_three_decimals_returns_422()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));

        var (status, _) = await NewSkuRawAsync(c, product, price: 10.999m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
    }

    [Fact]
    public async Task Negative_stock_returns_422()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));

        var (status, body) = await NewSkuRawAsync(c, product, stock: -1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("NEGATIVE_STOCK", Api.Code(body));
    }

    [Fact]
    public async Task Duplicate_sku_code_returns_409()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var code = Slug("DUPCODE").ToUpperInvariant();
        await NewSkuRawAsync(c, product, code, null, "US 8");

        var (status, body) = await NewSkuRawAsync(c, product, code, null, "US 9");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("DUPLICATE_SKU_CODE", Api.Code(body));
    }

    [Fact]
    public async Task Same_size_twice_for_same_variant_returns_409()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var variant = await NewVariantAsync(c, product);
        await NewSkuAsync(c, product, variant, "US 10");

        var (status, body) = await NewSkuRawAsync(c, product, null, variant, "US 10");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("DUPLICATE_SKU_COMBINATION", Api.Code(body));
    }

    [Fact]
    public async Task Same_size_twice_for_product_without_variant_returns_409()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        await NewSkuAsync(c, product, null, "US 9");

        var (status, body) = await NewSkuRawAsync(c, product, null, null, "US 9");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("DUPLICATE_SKU_COMBINATION", Api.Code(body));
    }

    [Fact]
    public async Task Sku_cannot_use_a_variant_of_another_product()
    {
        var c = await AdminAsync();
        var cat = await NewCategoryAsync(c);
        var productA = await NewProductAsync(c, cat);
        var productB = await NewProductAsync(c, cat);
        var variantOfA = await NewVariantAsync(c, productA);

        var (status, body) = await NewSkuRawAsync(c, productB, null, variantOfA);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("INVALID_VARIANT", Api.Code(body));
    }

    [Fact]
    public async Task Missing_combination_creates_no_row()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var variant = await NewVariantAsync(c, product, "Volt/Blue");
        await NewSkuAsync(c, product, variant, "US 9");   // US 10 intentionally not created

        var (_, body) = await Api.Get(c, $"/api/v1/admin/products/{product}");

        Assert.Single(body.GetProperty("skus").EnumerateArray());
    }

    [Fact]
    public async Task Stock_delta_that_goes_below_zero_returns_422_and_keeps_stock()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var sku = await NewSkuAsync(c, product, stock: 2);

        var (status, body) = await Api.Patch(c, $"/api/v1/admin/skus/{sku}", new { stockDelta = -3 });
        var (_, product2) = await Api.Get(c, $"/api/v1/admin/products/{product}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("NEGATIVE_STOCK", Api.Code(body));
        Assert.Equal(2, product2.GetProperty("skus")[0].GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Stock_delta_within_range_updates_stock()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var sku = await NewSkuAsync(c, product, stock: 5);

        var (status, body) = await Api.Patch(c, $"/api/v1/admin/skus/{sku}", new { stockDelta = -5 });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.GetProperty("stockQuantity").GetInt32());   // out of stock, but the row still exists
    }

    // ---- the database itself must refuse bad data, even if the API were bypassed (CAT05)

    [Fact]
    public async Task Database_check_constraint_rejects_negative_stock()
    {
        var c = await AdminAsync();
        var product = await NewProductAsync(c, await NewCategoryAsync(c));
        var sku = await NewSkuAsync(c, product);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => Factory.QueryAsync(db =>
            db.Database.ExecuteSqlRawAsync("UPDATE skus SET stock_quantity = -1 WHERE id = {0}", sku)));

        Assert.Contains("ck_skus_stock_nonneg", ex.ToString());
    }

    [Fact]
    public async Task Database_composite_foreign_key_rejects_variant_of_another_product()
    {
        var c = await AdminAsync();
        var cat = await NewCategoryAsync(c);
        var productA = await NewProductAsync(c, cat);
        var productB = await NewProductAsync(c, cat);
        var variantOfA = await NewVariantAsync(c, productA);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => Factory.QueryAsync(db =>
            db.Database.ExecuteSqlRawAsync(
                "INSERT INTO skus (product_id, variant_id, sku_code, size_label, price, stock_quantity, is_active) VALUES ({0}, {1}, {2}, 'US 9', 10.00, 1, 1)",
                productB, variantOfA, Slug("RAW").ToUpperInvariant())));

        Assert.Contains("foreign key", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
