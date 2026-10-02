using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SneakMart.Api.Data;
using SneakMart.Api.Domain;
using SneakMart.Api.Dtos;

namespace SneakMart.Api.Services;

public class CatalogService(AppDbContext db)
{
    // ------------------------------------------------------------------ helpers

    static ApiException NotFound(string what) => new(404, "NOT_FOUND", $"{what} was not found.");

    static ApiException Duplicate(string code, string field, string message) =>
        new(409, code, message, [new FieldError(field, message)]);

    static ApiException Unprocessable(string code, string field, string message) =>
        new(422, code, message, [new FieldError(field, message)]);

    static JsonElement? ParseJson(string? json)
    {
        if (json is null) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    static SkuDto ToDto(Sku s) =>
        new(s.Id, s.ProductId, s.VariantId, s.SkuCode, s.SizeLabel, s.Price, s.CompareAtPrice, s.StockQuantity, s.IsActive);

    static VariantDto ToDto(Variant v) =>
        new(v.Id, v.ProductId, v.Name, v.Color, ParseJson(v.OptionValues), v.CreatedAt);

    static CategoryDto ToDto(Category c) =>
        new(c.Id, c.ParentId, c.Name, c.Slug, c.Description, c.IsActive, c.CreatedAt);

    static ProductDto ToDto(Product p) =>
        new(p.Id, p.CategoryId, p.Name, p.Slug, p.Brand, p.Description, p.Status, ParseJson(p.Specifications),
            p.Variants.OrderBy(v => v.Id).Select(ToDto).ToList(),
            p.Skus.OrderBy(s => s.Id).Select(ToDto).ToList(),
            p.CreatedAt, p.UpdatedAt);

    static void ValidateMoney(decimal value, string field)
    {
        if (value < 0) throw Unprocessable("VALIDATION_ERROR", field, $"{field} must not be negative.");
        if (decimal.Round(value, 2) != value) throw Unprocessable("VALIDATION_ERROR", field, $"{field} must have at most 2 decimal places.");
        if (value > 99_999_999.99m) throw Unprocessable("VALIDATION_ERROR", field, $"{field} is too large.");
    }

    // ------------------------------------------------------------------ categories (CAT01)

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest r, CancellationToken ct)
    {
        if (r.ParentId is int pid && !await db.Categories.AnyAsync(c => c.Id == pid, ct))
            throw NotFound("Parent category");

        if (await db.Categories.AnyAsync(c => c.Slug == r.Slug, ct))
            throw Duplicate("DUPLICATE_SLUG", "slug", $"Category slug '{r.Slug}' is already in use.");

        var cat = new Category { Name = r.Name!, Slug = r.Slug!, ParentId = r.ParentId, Description = r.Description };
        db.Categories.Add(cat);
        await db.SaveChangesAsync(ct);
        return ToDto(cat);
    }

    public async Task<List<CategoryNode>> GetCategoryTreeAsync(CancellationToken ct)
    {
        var all = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var nodes = all.ToDictionary(c => c.Id, c => new CategoryNode(c.Id, c.ParentId, c.Name, c.Slug, c.IsActive, []));
        var roots = new List<CategoryNode>();
        foreach (var c in all)
        {
            if (c.ParentId is int p && nodes.TryGetValue(p, out var parent)) parent.Children.Add(nodes[c.Id]);
            else roots.Add(nodes[c.Id]);
        }
        return roots;
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, UpdateCategoryRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var all = await db.Categories.ToListAsync(ct);
        var byId = all.ToDictionary(c => c.Id);
        if (!byId.TryGetValue(id, out var cat)) throw NotFound("Category");

        if (r.Slug is not null && r.Slug != cat.Slug)
        {
            if (all.Any(c => c.Slug == r.Slug && c.Id != id))
                throw Duplicate("DUPLICATE_SLUG", "slug", $"Category slug '{r.Slug}' is already in use.");
            cat.Slug = r.Slug;
        }
        if (r.Name is not null) cat.Name = r.Name;
        if (r.Description is not null) cat.Description = r.Description;

        if (r.MakeRoot == true)
        {
            cat.ParentId = null;
        }
        else if (r.ParentId is int newParent)
        {
            if (!byId.ContainsKey(newParent)) throw NotFound("Parent category");

            // Cycle prevention: a category can never become its own ancestor.
            int? walk = newParent;
            while (walk is int w)
            {
                if (w == id)
                    throw Unprocessable("CATEGORY_CYCLE", "parentId", "A category cannot be its own ancestor.");
                walk = byId[w].ParentId;
            }
            cat.ParentId = newParent;
        }

        if (r.IsActive is bool active)
        {
            if (active) cat.IsActive = true;
            else
            {
                // Deactivating a parent deactivates all descendants (same transaction).
                var queue = new Queue<Category>([cat]);
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    cur.IsActive = false;
                    foreach (var child in all.Where(c => c.ParentId == cur.Id)) queue.Enqueue(child);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(cat);
    }

    // ------------------------------------------------------------------ products (CAT02)

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest r, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == r.CategoryId, ct)) throw NotFound("Category");
        if (await db.Products.AnyAsync(p => p.Slug == r.Slug, ct))
            throw Duplicate("DUPLICATE_SLUG", "slug", $"Product slug '{r.Slug}' is already in use.");

        var p = new Product
        {
            CategoryId = r.CategoryId!.Value, Name = r.Name!, Slug = r.Slug!, Brand = r.Brand!,
            Description = r.Description, Status = ProductStatus.Draft
        };
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<PagedResult<ProductDto>> ListProductsAsync(int page, int pageSize, string? status, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (status is not null && !ProductStatus.All.Contains(status))
            throw Unprocessable("VALIDATION_ERROR", "status", "status must be draft, active or archived.");

        var q = db.Products.AsNoTracking().AsQueryable();
        if (status is not null) q = q.Where(p => p.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(p => p.Variants).Include(p => p.Skus).AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<ProductDto> GetProductAsync(int id, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking()
            .Include(x => x.Variants).Include(x => x.Skus).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? throw NotFound("Product") : ToDto(p);
    }

    public async Task<ProductDto> UpdateProductAsync(int id, UpdateProductRequest r, CancellationToken ct)
    {
        var p = await db.Products.Include(x => x.Variants).Include(x => x.Skus).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("Product");

        if (r.Slug is not null && r.Slug != p.Slug)
        {
            if (await db.Products.AnyAsync(x => x.Slug == r.Slug && x.Id != id, ct))
                throw Duplicate("DUPLICATE_SLUG", "slug", $"Product slug '{r.Slug}' is already in use.");
            p.Slug = r.Slug;
        }
        if (r.CategoryId is int cid && cid != p.CategoryId)
        {
            if (!await db.Categories.AnyAsync(c => c.Id == cid, ct)) throw NotFound("Category");
            p.CategoryId = cid;
        }
        if (r.Name is not null) p.Name = r.Name;
        if (r.Brand is not null) p.Brand = r.Brand;
        if (r.Description is not null) p.Description = r.Description;

        if (r.Status is not null)
        {
            if (!ProductStatus.All.Contains(r.Status))
                throw Unprocessable("VALIDATION_ERROR", "status", "status must be draft, active or archived.");
            if (r.Status == ProductStatus.Active && !p.Skus.Any(s => s.IsActive))
                throw Unprocessable("PRODUCT_HAS_NO_ACTIVE_SKU", "status", "A product needs at least one active SKU before it can be set to active.");
            p.Status = r.Status;
        }

        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    // ------------------------------------------------------------------ variants & SKUs (CAT03, CAT04)

    public async Task<VariantDto> AddVariantAsync(int productId, CreateVariantRequest r, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct)) throw NotFound("Product");
        if (await db.Variants.AnyAsync(v => v.ProductId == productId && v.Name == r.Name, ct))
            throw Duplicate("DUPLICATE_VARIANT", "name", $"Variant '{r.Name}' already exists for this product.");

        string? options = null;
        if (r.OptionValues is { } el && el.ValueKind != JsonValueKind.Null)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw Unprocessable("VALIDATION_ERROR", "optionValues", "optionValues must be a JSON object.");
            options = el.GetRawText();
        }

        var v = new Variant { ProductId = productId, Name = r.Name!, Color = r.Color!, OptionValues = options };
        db.Variants.Add(v);
        await db.SaveChangesAsync(ct);
        return ToDto(v);
    }

    public async Task<SkuDto> AddSkuAsync(int productId, CreateSkuRequest r, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct)) throw NotFound("Product");

        var price = r.Price!.Value;
        var stock = r.StockQuantity!.Value;
        ValidateMoney(price, "price");
        if (r.CompareAtPrice is decimal cmp)
        {
            ValidateMoney(cmp, "compareAtPrice");
            if (cmp < price) throw Unprocessable("VALIDATION_ERROR", "compareAtPrice", "compareAtPrice must be >= price.");
        }
        if (stock < 0) throw Unprocessable("NEGATIVE_STOCK", "stockQuantity", "stockQuantity must not be negative.");

        var code = r.SkuCode!.Trim();
        var size = r.SizeLabel!.Trim();

        if (r.VariantId is int vid && !await db.Variants.AnyAsync(v => v.Id == vid && v.ProductId == productId, ct))
            throw Unprocessable("INVALID_VARIANT", "variantId", "The variant does not exist for this product.");

        if (await db.Skus.AnyAsync(s => s.SkuCode == code, ct))
            throw Duplicate("DUPLICATE_SKU_CODE", "skuCode", $"SKU code '{code}' is already in use.");

        var variantId = r.VariantId;
        if (await db.Skus.AnyAsync(s => s.ProductId == productId && s.VariantId == variantId && s.SizeLabel == size, ct))
            throw Duplicate("DUPLICATE_SKU_COMBINATION", "sizeLabel", $"Size '{size}' already exists for this product/variant.");

        var sku = new Sku
        {
            ProductId = productId, VariantId = variantId, SkuCode = code, SizeLabel = size,
            Price = price, CompareAtPrice = r.CompareAtPrice, StockQuantity = stock, IsActive = true
        };
        db.Skus.Add(sku);
        await db.SaveChangesAsync(ct);
        return ToDto(sku);
    }

    public async Task<SkuDto> UpdateSkuAsync(int id, UpdateSkuRequest r, CancellationToken ct)
    {
        var sku = await db.Skus.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFound("SKU");

        if (r.StockQuantity is not null && r.StockDelta is not null)
            throw Unprocessable("VALIDATION_ERROR", "stockDelta", "Send either stockQuantity or stockDelta, not both.");

        if (r.Price is decimal price)
        {
            ValidateMoney(price, "price");
            sku.Price = price;
        }
        if (r.CompareAtPrice is decimal cmp)
        {
            ValidateMoney(cmp, "compareAtPrice");
            sku.CompareAtPrice = cmp;
        }
        if (sku.CompareAtPrice is decimal c2 && c2 < sku.Price)
            throw Unprocessable("VALIDATION_ERROR", "compareAtPrice", "compareAtPrice must be >= price.");

        if (r.StockQuantity is int absolute)
        {
            if (absolute < 0) throw Unprocessable("NEGATIVE_STOCK", "stockQuantity", "stockQuantity must not be negative.");
            sku.StockQuantity = absolute;
        }
        if (r.StockDelta is int delta)
        {
            var result = (long)sku.StockQuantity + delta;
            if (result < 0) throw Unprocessable("NEGATIVE_STOCK", "stockDelta", "This adjustment would make stock negative.");
            sku.StockQuantity = (int)result;
        }

        if (r.IsActive is bool active)
        {
            sku.IsActive = active;
            if (!active)
            {
                // A product cannot stay 'active' without any active SKU: fall back to draft.
                var anyOtherActive = await db.Skus.AnyAsync(s => s.ProductId == sku.ProductId && s.Id != sku.Id && s.IsActive, ct);
                if (!anyOtherActive)
                {
                    var product = await db.Products.FirstAsync(p => p.Id == sku.ProductId, ct);
                    if (product.Status == ProductStatus.Active) product.Status = ProductStatus.Draft;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return ToDto(sku);
    }
}
