namespace SneakMart.Api.Domain;

public static class Roles
{
    public const string Admin = "admin";
    public const string Customer = "customer";
}

public static class ProductStatus
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Archived = "archived";
    public static readonly string[] All = [Draft, Active, Archived];
}

public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}

// ---------------- Sprint 1 entities (unchanged except where noted) ----------------

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Customer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public List<CartItem> Items { get; set; } = [];
}

/// <summary>Sprint 2 change: references a SKU, not a Product.</summary>
public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int SkuId { get; set; }
    public int Quantity { get; set; }
    public Cart Cart { get; set; } = null!;
    public Sku Sku { get; set; } = null!;
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "pending";
    public string? ShippingAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = [];
}

/// <summary>Sprint 2 change: references a SKU and keeps snapshots of what was sold.</summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SkuId { get; set; }
    public string ProductNameSnapshot { get; set; } = "";
    public string SkuCodeSnapshot { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Order Order { get; set; } = null!;
    public Sku Sku { get; set; } = null!;
}

// ---------------- Sprint 2 catalog entities ----------------

public class Category : ITimestamped
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = [];
    public List<Product> Products { get; set; } = [];
}

public class Product : ITimestamped
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Brand { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = ProductStatus.Draft;
    /// <summary>JSON object (validated in Sprint 3). Column exists now.</summary>
    public string? Specifications { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Category Category { get; set; } = null!;
    public List<Variant> Variants { get; set; } = [];
    public List<Sku> Skus { get; set; } = [];
    public List<Asset> Assets { get; set; } = [];
}

/// <summary>A colorway of a product.</summary>
public class Variant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    /// <summary>Optional JSON object with extra options.</summary>
    public string? OptionValues { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Product Product { get; set; } = null!;
    public List<Sku> Skus { get; set; } = [];
}

/// <summary>The sellable unit: product + (optional) variant + size.</summary>
public class Sku : ITimestamped
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string SkuCode { get; set; } = "";
    public string SizeLabel { get; set; } = "";
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>DB-generated: IFNULL(variant_id, 0). Used for the unique index.</summary>
    public int VariantKey { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Product Product { get; set; } = null!;
    public Variant? Variant { get; set; }
}

/// <summary>Table only in Sprint 2. Upload endpoints arrive in Sprint 3.</summary>
public class Asset
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string StorageKey { get; set; } = "";
    public string Role { get; set; } = "gallery";
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public Product Product { get; set; } = null!;
    public Variant? Variant { get; set; }
}
