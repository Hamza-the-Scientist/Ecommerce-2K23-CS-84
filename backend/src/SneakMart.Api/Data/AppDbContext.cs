using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SneakMart.Api.Domain;

namespace SneakMart.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Variant> Variants => Set<Variant>();
    public DbSet<Sku> Skus => Set<Sku>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        const string now = "CURRENT_TIMESTAMP(6)";

        mb.Entity<User>(b =>
        {
            b.ToTable("users", t => t.HasCheckConstraint("ck_users_role", "role IN ('customer','admin')"));
            b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Email).HasMaxLength(150).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            b.Property(x => x.Role).HasMaxLength(20).IsRequired();
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_users_email");
        });

        mb.Entity<Category>(b =>
        {
            b.ToTable("categories", t => t.HasCheckConstraint("ck_categories_not_self_parent", "parent_id IS NULL OR parent_id <> id"));
            b.Property(x => x.Name).HasMaxLength(50).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            b.Property(x => x.Description).HasMaxLength(255);
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.Property(x => x.UpdatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_categories_slug");
            b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Product>(b =>
        {
            b.ToTable("products", t =>
            {
                t.HasCheckConstraint("ck_products_status", "status IN ('draft','active','archived')");
                t.HasCheckConstraint("ck_products_specs_object", "specifications IS NULL OR JSON_TYPE(specifications) = 'OBJECT'");
            });
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            b.Property(x => x.Brand).HasMaxLength(50).IsRequired();
            b.Property(x => x.Description).HasColumnType("text");
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.Specifications).HasColumnType("json");
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.Property(x => x.UpdatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ux_products_slug");
            b.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Variant>(b =>
        {
            b.ToTable("variants", t => t.HasCheckConstraint("ck_variants_options_object", "option_values IS NULL OR JSON_TYPE(option_values) = 'OBJECT'"));
            b.Property(x => x.Name).HasMaxLength(80).IsRequired();
            b.Property(x => x.Color).HasMaxLength(40).IsRequired();
            b.Property(x => x.OptionValues).HasColumnType("json");
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.HasIndex(x => new { x.ProductId, x.Name }).IsUnique().HasDatabaseName("ux_variants_product_name");
            // Target of the composite FK from skus: guarantees a SKU's variant belongs to the SKU's product.
            b.HasAlternateKey(x => new { x.Id, x.ProductId }).HasName("ux_variants_id_product");
            b.HasOne(x => x.Product).WithMany(p => p.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Sku>(b =>
        {
            b.ToTable("skus", t =>
            {
                t.HasCheckConstraint("ck_skus_price_nonneg", "price >= 0");
                t.HasCheckConstraint("ck_skus_stock_nonneg", "stock_quantity >= 0");
                t.HasCheckConstraint("ck_skus_compare_price", "compare_at_price IS NULL OR compare_at_price >= price");
            });
            b.Property(x => x.SkuCode).HasMaxLength(40).IsRequired();
            b.Property(x => x.SizeLabel).HasMaxLength(10).IsRequired();
            b.Property(x => x.Price).HasColumnType("decimal(10,2)");
            b.Property(x => x.CompareAtPrice).HasColumnType("decimal(10,2)");
            b.Property(x => x.VariantKey).HasComputedColumnSql("IFNULL(`variant_id`, 0)", stored: true);
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.Property(x => x.UpdatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.HasIndex(x => x.SkuCode).IsUnique().HasDatabaseName("ux_skus_sku_code");
            b.HasIndex(x => new { x.ProductId, x.VariantKey, x.SizeLabel }).IsUnique().HasDatabaseName("ux_skus_combo");
            b.HasOne(x => x.Product).WithMany(p => p.Skus).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Variant).WithMany(v => v.Skus)
                .HasForeignKey(x => new { x.VariantId, x.ProductId })
                .HasPrincipalKey(v => new { v.Id, v.ProductId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Asset>(b =>
        {
            b.ToTable("assets");
            b.Property(x => x.StorageKey).HasMaxLength(255).IsRequired();
            b.Property(x => x.Role).HasMaxLength(20).IsRequired();
            b.Property(x => x.AltText).HasMaxLength(255);
            b.HasOne(x => x.Product).WithMany(p => p.Assets).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<Cart>(b =>
        {
            b.ToTable("cart");
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_cart_user");
            b.HasOne(x => x.User).WithOne().HasForeignKey<Cart>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<CartItem>(b =>
        {
            b.ToTable("cart_items", t => t.HasCheckConstraint("ck_cart_items_qty", "quantity > 0"));
            b.HasIndex(x => new { x.CartId, x.SkuId }).IsUnique().HasDatabaseName("ux_cart_items_cart_sku");
            b.HasOne(x => x.Cart).WithMany(c => c.Items).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Sku).WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.Property(x => x.TotalAmount).HasColumnType("decimal(10,2)");
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.ShippingAddress).HasMaxLength(255);
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql(now);
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<OrderItem>(b =>
        {
            b.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_qty", "quantity > 0"));
            b.Property(x => x.ProductNameSnapshot).HasMaxLength(150).IsRequired();
            b.Property(x => x.SkuCodeSnapshot).HasMaxLength(40).IsRequired();
            b.Property(x => x.UnitPrice).HasColumnType("decimal(10,2)");
            b.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Sku).WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
        });

        // snake_case column names (FullName -> full_name) to match the documented data dictionary
        foreach (var entity in mb.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
                prop.SetColumnName(Regex.Replace(prop.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in ChangeTracker.Entries<ITimestamped>().Where(e => e.State == EntityState.Modified))
            e.Entity.UpdatedAt = DateTime.UtcNow;
        return base.SaveChangesAsync(cancellationToken);
    }
}
