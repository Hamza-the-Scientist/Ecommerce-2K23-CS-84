using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SneakMart.Api.Dtos;

public static class Rules
{
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";
    public const string SlugMessage = "Slug must be lowercase letters/digits separated by single hyphens.";
}

// ---------------- requests ----------------

public class LoginRequest
{
    [Required] public string? Email { get; set; }
    [Required] public string? Password { get; set; }
}

public class CreateCategoryRequest
{
    [Required, StringLength(50)] public string? Name { get; set; }
    [Required, StringLength(80), RegularExpression(Rules.SlugPattern, ErrorMessage = Rules.SlugMessage)] public string? Slug { get; set; }
    public int? ParentId { get; set; }
    [StringLength(255)] public string? Description { get; set; }
}

public class UpdateCategoryRequest
{
    [StringLength(50, MinimumLength = 1)] public string? Name { get; set; }
    [StringLength(80), RegularExpression(Rules.SlugPattern, ErrorMessage = Rules.SlugMessage)] public string? Slug { get; set; }
    public int? ParentId { get; set; }
    /// <summary>Set true to move the category to the root (ParentId cannot express "null" in a PATCH).</summary>
    public bool? MakeRoot { get; set; }
    [StringLength(255)] public string? Description { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateProductRequest
{
    [Required] public int? CategoryId { get; set; }
    [Required, StringLength(150)] public string? Name { get; set; }
    [Required, StringLength(180), RegularExpression(Rules.SlugPattern, ErrorMessage = Rules.SlugMessage)] public string? Slug { get; set; }
    [Required, StringLength(50)] public string? Brand { get; set; }
    public string? Description { get; set; }
}

public class UpdateProductRequest
{
    public int? CategoryId { get; set; }
    [StringLength(150, MinimumLength = 1)] public string? Name { get; set; }
    [StringLength(180), RegularExpression(Rules.SlugPattern, ErrorMessage = Rules.SlugMessage)] public string? Slug { get; set; }
    [StringLength(50, MinimumLength = 1)] public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public class CreateVariantRequest
{
    [Required, StringLength(80)] public string? Name { get; set; }
    [Required, StringLength(40)] public string? Color { get; set; }
    public JsonElement? OptionValues { get; set; }
}

public class CreateSkuRequest
{
    [Required, StringLength(40)] public string? SkuCode { get; set; }
    public int? VariantId { get; set; }
    [Required, StringLength(10)] public string? SizeLabel { get; set; }
    [Required] public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    [Required] public int? StockQuantity { get; set; }
}

public class UpdateSkuRequest
{
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    /// <summary>Absolute stock value.</summary>
    public int? StockQuantity { get; set; }
    /// <summary>Relative stock adjustment (+/-). Cannot be combined with StockQuantity.</summary>
    public int? StockDelta { get; set; }
    public bool? IsActive { get; set; }
}

// ---------------- responses ----------------

public record AuthResponse(string Token, int ExpiresInSeconds, string Role);

public record CategoryDto(int Id, int? ParentId, string Name, string Slug, string? Description, bool IsActive, DateTime CreatedAt);

public record CategoryNode(int Id, int? ParentId, string Name, string Slug, bool IsActive, List<CategoryNode> Children);

public record SkuDto(int Id, int ProductId, int? VariantId, string SkuCode, string SizeLabel, decimal Price,
    decimal? CompareAtPrice, int StockQuantity, bool IsActive);

public record VariantDto(int Id, int ProductId, string Name, string Color, JsonElement? OptionValues, DateTime CreatedAt);

public record ProductDto(int Id, int CategoryId, string Name, string Slug, string Brand, string? Description, string Status,
    JsonElement? Specifications, List<VariantDto> Variants, List<SkuDto> Skus, DateTime CreatedAt, DateTime UpdatedAt);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int Total);
