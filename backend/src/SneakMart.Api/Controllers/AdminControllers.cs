using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SneakMart.Api.Domain;
using SneakMart.Api.Dtos;
using SneakMart.Api.Services;

namespace SneakMart.Api.Controllers;

[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Roles = Roles.Admin)]
public class AdminCategoriesController(CatalogService catalog) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateCategoryAsync(request, ct);
        return Created($"/api/v1/admin/categories/{created.Id}", created);
    }

    [HttpGet]
    public async Task<IActionResult> Tree(CancellationToken ct) => Ok(await catalog.GetCategoryTreeAsync(ct));

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCategoryRequest request, CancellationToken ct) =>
        Ok(await catalog.UpdateCategoryAsync(id, request, ct));
}

[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Roles = Roles.Admin)]
public class AdminProductsController(CatalogService catalog) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateProductAsync(request, ct);
        return Created($"/api/v1/admin/products/{created.Id}", created);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, CancellationToken ct = default) =>
        Ok(await catalog.ListProductsAsync(page, pageSize, status, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await catalog.GetProductAsync(id, ct));

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken ct) =>
        Ok(await catalog.UpdateProductAsync(id, request, ct));

    [HttpPost("{id:int}/variants")]
    public async Task<IActionResult> AddVariant(int id, CreateVariantRequest request, CancellationToken ct)
    {
        var created = await catalog.AddVariantAsync(id, request, ct);
        return Created($"/api/v1/admin/products/{id}", created);
    }

    [HttpPost("{id:int}/skus")]
    public async Task<IActionResult> AddSku(int id, CreateSkuRequest request, CancellationToken ct)
    {
        var created = await catalog.AddSkuAsync(id, request, ct);
        return Created($"/api/v1/admin/skus/{created.Id}", created);
    }
}

[ApiController]
[Route("api/v1/admin/skus")]
[Authorize(Roles = Roles.Admin)]
public class AdminSkusController(CatalogService catalog) : ControllerBase
{
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateSkuRequest request, CancellationToken ct) =>
        Ok(await catalog.UpdateSkuAsync(id, request, ct));
}
