using System.Net;
using Xunit;

namespace SneakMart.Tests;

public class CategoryTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_category_with_parent_returns_201_and_appears_in_tree()
    {
        var c = await AdminAsync();
        var rootId = await NewCategoryAsync(c);
        var childId = await NewCategoryAsync(c, rootId);

        var (status, tree) = await Api.Get(c, "/api/v1/admin/categories");
        Assert.Equal(HttpStatusCode.OK, status);

        var root = tree.EnumerateArray().Single(n => n.GetProperty("id").GetInt32() == rootId);
        Assert.Contains(root.GetProperty("children").EnumerateArray(), ch => ch.GetProperty("id").GetInt32() == childId);
    }

    [Fact]
    public async Task Duplicate_category_slug_returns_409()
    {
        var c = await AdminAsync();
        var slug = Slug("dup");
        await NewCategoryAsync(c, null, slug);

        var (status, body) = await Api.Post(c, "/api/v1/admin/categories", new { name = "Again", slug });
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("DUPLICATE_SLUG", Api.Code(body));
    }

    [Fact]
    public async Task Category_cannot_be_its_own_parent()
    {
        var c = await AdminAsync();
        var id = await NewCategoryAsync(c);

        var (status, body) = await Api.Patch(c, $"/api/v1/admin/categories/{id}", new { parentId = id });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("CATEGORY_CYCLE", Api.Code(body));
    }

    [Fact]
    public async Task Category_cannot_become_its_own_ancestor()
    {
        var c = await AdminAsync();
        var a = await NewCategoryAsync(c);
        var b = await NewCategoryAsync(c, a);
        var d = await NewCategoryAsync(c, b);

        // A -> B -> D ; moving A under D would create a cycle
        var (status, body) = await Api.Patch(c, $"/api/v1/admin/categories/{a}", new { parentId = d });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("CATEGORY_CYCLE", Api.Code(body));
    }

    [Fact]
    public async Task Deactivating_parent_deactivates_all_descendants()
    {
        var c = await AdminAsync();
        var a = await NewCategoryAsync(c);
        var b = await NewCategoryAsync(c, a);
        var d = await NewCategoryAsync(c, b);

        var (status, _) = await Api.Patch(c, $"/api/v1/admin/categories/{a}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, status);

        var active = await Factory.QueryAsync(db => Task.FromResult(
            db.Categories.Where(x => x.Id == a || x.Id == b || x.Id == d).Select(x => x.IsActive).ToList()));
        Assert.All(active, isActive => Assert.False(isActive));
    }

    [Fact]
    public async Task Category_with_unknown_parent_returns_404()
    {
        var c = await AdminAsync();
        var (status, body) = await Api.Post(c, "/api/v1/admin/categories", new { name = "X", slug = Slug("x"), parentId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("NOT_FOUND", Api.Code(body));
    }
}
