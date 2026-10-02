using Microsoft.EntityFrameworkCore;
using SneakMart.Api.Data;
using Xunit;

namespace SneakMart.Tests;

public class SeedTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Seed_produces_the_documented_demonstration_data_and_is_repeatable()
    {
        // run twice: the seed must be idempotent
        await Factory.QueryAsync(async db => { await SeedData.RunAsync(db, "Seed#12345"); return 0; });
        await Factory.QueryAsync(async db => { await SeedData.RunAsync(db, "Seed#12345"); return 0; });

        await Factory.QueryAsync(async db =>
        {
            Assert.Equal(4, await db.Categories.CountAsync());
            Assert.True(await db.Categories.AnyAsync(c => c.ParentId != null));          // two levels
            Assert.Equal(3, await db.Products.CountAsync());
            Assert.Equal(6, await db.Skus.CountAsync());
            Assert.True(await db.Skus.AnyAsync(s => s.StockQuantity == 0));              // out-of-stock SKU exists
            Assert.True(await db.Skus.AnyAsync(s => s.VariantId == null));               // zero-variant product
            Assert.True(await db.Variants.GroupBy(v => v.ProductId).AnyAsync(g => g.Count() > 1)); // multi-variant product
            return 0;
        });

        // Intentionally unavailable combination: Volt/Blue US 10 has no row.
        var missing = await Factory.QueryAsync(db =>
            db.Skus.AnyAsync(s => s.Variant!.Name == "Volt/Blue" && s.SizeLabel == "US 10"));
        Assert.False(missing);
    }

    [Fact]
    public async Task Seeded_admin_can_log_in()
    {
        await Factory.QueryAsync(async db => { await SeedData.RunAsync(db, "Seed#12345"); return 0; });

        var client = await Factory.ClientForAsync(SeedData.AdminEmail, "Seed#12345");
        var (status, _) = await Api.Get(client, "/api/v1/admin/products");

        Assert.Equal(System.Net.HttpStatusCode.OK, status);
    }
}
