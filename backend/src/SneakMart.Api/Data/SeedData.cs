using Microsoft.EntityFrameworkCore;
using SneakMart.Api.Domain;

namespace SneakMart.Api.Data;

/// <summary>
/// Reproducible demonstration data. Idempotent: clears catalog-dependent tables, then re-inserts.
/// Run: dotnet run --project src/SneakMart.Api -- --seed   (requires SEED_ADMIN_PASSWORD)
/// </summary>
public static class SeedData
{
    public const string AdminEmail = "admin@sneakmart.local";

    public static async Task RunAsync(AppDbContext db, string adminPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminPassword)) throw new InvalidOperationException("Admin password is required for seeding.");

        // ---- clear (children first)
        await db.OrderItems.ExecuteDeleteAsync(ct);
        await db.Orders.ExecuteDeleteAsync(ct);
        await db.CartItems.ExecuteDeleteAsync(ct);
        await db.Assets.ExecuteDeleteAsync(ct);
        await db.Skus.ExecuteDeleteAsync(ct);
        await db.Variants.ExecuteDeleteAsync(ct);
        await db.Products.ExecuteDeleteAsync(ct);
        await db.Categories.ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentId, (int?)null), ct); // avoid self-FK ordering issues
        await db.Categories.ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.Email == AdminEmail).ExecuteDeleteAsync(ct);
        db.ChangeTracker.Clear();

        // ---- admin
        db.Users.Add(new User
        {
            FullName = "SneakMart Admin", Email = AdminEmail, Role = Roles.Admin,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword)
        });

        // ---- category tree (2 levels)
        var footwear = new Category { Name = "Footwear", Slug = "footwear" };
        var running = new Category { Name = "Running", Slug = "running", Parent = footwear };
        var basketball = new Category { Name = "Basketball", Slug = "basketball", Parent = footwear };
        var lifestyle = new Category { Name = "Lifestyle", Slug = "lifestyle", Parent = footwear };
        db.Categories.AddRange(footwear, running, basketball, lifestyle);

        // ---- product 1: multiple variants, one intentionally missing combination
        var pegasus = new Product
        {
            Category = running, Name = "Nike Air Zoom Pegasus 41", Slug = "nike-air-zoom-pegasus-41", Brand = "Nike",
            Description = "Everyday road running shoe.", Status = ProductStatus.Active,
            Specifications = """{"upper":"engineered mesh","drop_mm":10,"waterproof":false}"""
        };
        var pegBW = new Variant { Product = pegasus, Name = "Black/White", Color = "Black" };
        var pegVB = new Variant { Product = pegasus, Name = "Volt/Blue", Color = "Volt" };
        db.Skus.AddRange(
            new Sku { Product = pegasus, Variant = pegBW, SkuCode = "PEG41-BW-9", SizeLabel = "US 9", Price = 129.99m, StockQuantity = 10 },
            new Sku { Product = pegasus, Variant = pegBW, SkuCode = "PEG41-BW-10", SizeLabel = "US 10", Price = 129.99m, StockQuantity = 8 },
            new Sku { Product = pegasus, Variant = pegVB, SkuCode = "PEG41-VB-9", SizeLabel = "US 9", Price = 134.99m, StockQuantity = 5 }
            // Volt/Blue in US 10 is intentionally NOT created: that combination is not produced (no fake zero-stock row).
        );

        // ---- product 2: one SKU out of stock (row exists, stock = 0)
        var jordan = new Product
        {
            Category = basketball, Name = "Air Jordan 1 Retro High OG", Slug = "air-jordan-1-retro-high-og", Brand = "Jordan",
            Description = "Collector release.", Status = ProductStatus.Active
        };
        var chicago = new Variant { Product = jordan, Name = "Chicago", Color = "Red/White/Black" };
        db.Skus.AddRange(
            new Sku { Product = jordan, Variant = chicago, SkuCode = "AJ1-CHI-9", SizeLabel = "US 9", Price = 179.99m, StockQuantity = 0 },
            new Sku { Product = jordan, Variant = chicago, SkuCode = "AJ1-CHI-11", SizeLabel = "US 11", Price = 179.99m, StockQuantity = 3 }
        );

        // ---- product 3: zero-variant product (SKU without a variant)
        var samba = new Product
        {
            Category = lifestyle, Name = "Adidas Samba OG", Slug = "adidas-samba-og", Brand = "Adidas",
            Description = "Classic terrace shoe.", Status = ProductStatus.Active
        };
        db.Skus.Add(new Sku { Product = samba, SkuCode = "SAMBA-OG-9", SizeLabel = "US 9", Price = 99.99m, StockQuantity = 12 });

        await db.SaveChangesAsync(ct);
    }
}
