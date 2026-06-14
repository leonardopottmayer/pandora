using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers the category endpoints: the seeded system tree (read-only, two levels) and the user
/// category CRUD with its hierarchy/nature rules, (de)activation, and audit trail.
/// </summary>
[Collection("Integration")]
public sealed class CategoriesTests : IAsyncLifetime
{
    private const string SystemUrl = "/api/v1/finances/categories/system";
    private const string UserUrl = "/api/v1/finances/categories";
    private const string AuditUrl = "/api/v1/finances/audit";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CategoriesTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task System_tree_returns_two_levels_ordered()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "sys@example.com", "sys");

        var tree = await GetSystemAsync();

        Assert.NotEmpty(tree);
        Assert.True(tree.SequenceEqual(tree.OrderBy(c => c.DisplayOrder)), "roots must be ordered by displayOrder");

        var housing = Assert.Single(tree, c => c.Code == "housing");
        Assert.Equal("expense", housing.Nature);
        Assert.NotEmpty(housing.Children);
        Assert.Contains(housing.Children, c => c.IsOther); // each group has an "other" fallback child
        Assert.All(housing.Children, c => Assert.Empty(c.Children)); // exactly two levels
    }

    [Fact]
    public async Task System_tree_filters_by_nature()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "sysnat@example.com", "sysnat");

        var income = await GetSystemAsync("?nature=income");

        Assert.NotEmpty(income);
        Assert.All(income, c => Assert.Equal("income", c.Nature));
    }

    [Fact]
    public async Task Create_root_and_child_then_list_as_tree()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "cat1@example.com", "cat1");

        var root = await CreateAsync(new { name = "Side Projects", nature = "income", displayOrder = 1 });
        Assert.Equal(HttpStatusCode.OK, root.status);

        var child = await CreateAsync(new { name = "Consulting", nature = "income", parentCategoryId = root.dto!.Id, displayOrder = 1 });
        Assert.Equal(HttpStatusCode.OK, child.status);
        Assert.Equal(root.dto.Id, child.dto!.ParentCategoryId);

        var tree = await GetUserAsync();
        var r = Assert.Single(tree, c => c.Id == root.dto.Id);
        Assert.Single(r.Children, c => c.Id == child.dto.Id);
    }

    [Fact]
    public async Task Cannot_create_third_level()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "cat2@example.com", "cat2");

        var root = await CreateAsync(new { name = "Root", nature = "expense", displayOrder = 1 });
        var child = await CreateAsync(new { name = "Child", nature = "expense", parentCategoryId = root.dto!.Id, displayOrder = 1 });

        var grandchild = await CreateAsync(new { name = "Grandchild", nature = "expense", parentCategoryId = child.dto!.Id, displayOrder = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, grandchild.status);
    }

    [Fact]
    public async Task Child_nature_must_match_parent()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "cat3@example.com", "cat3");

        var root = await CreateAsync(new { name = "Expenses", nature = "expense", displayOrder = 1 });
        var child = await CreateAsync(new { name = "Mismatch", nature = "income", parentCategoryId = root.dto!.Id, displayOrder = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, child.status);
    }

    [Fact]
    public async Task Duplicate_name_under_same_parent_is_rejected()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "cat4@example.com", "cat4");

        var first = await CreateAsync(new { name = "Groceries", nature = "expense", displayOrder = 1 });
        Assert.Equal(HttpStatusCode.OK, first.status);

        var dup = await CreateAsync(new { name = "Groceries", nature = "expense", displayOrder = 2 });
        Assert.Equal(HttpStatusCode.Conflict, dup.status);
    }

    [Fact]
    public async Task Deactivate_hides_from_default_list_and_reactivates()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "cat5@example.com", "cat5");

        var cat = await CreateAsync(new { name = "Temporary", nature = "expense", displayOrder = 1 });
        var id = cat.dto!.Id;

        var deactivate = await _client.PostAsync($"{UserUrl}/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        Assert.DoesNotContain(await GetUserAsync(), c => c.Id == id);
        Assert.Contains(await GetUserAsync("?includeInactive=true"), c => c.Id == id);

        var reactivate = await _client.PostAsync($"{UserUrl}/{id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.Contains(await GetUserAsync(), c => c.Id == id);
    }

    [Fact]
    public async Task Mutations_are_audited()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "cat6@example.com", "cat6");

        var cat = await CreateAsync(new { name = "Audited", nature = "expense", displayOrder = 1 });
        var id = cat.dto!.Id;
        await _client.PutAsJsonAsync($"{UserUrl}/{id}", new { name = "Audited v2", displayOrder = 2 });
        await _client.PostAsync($"{UserUrl}/{id}/deactivate", null);

        var audit = await _client.GetFromJsonAsync<AuditEnvelope>(
            $"{AuditUrl}?entityType=user-category&entityId={id}");

        var types = audit!.Data.Select(e => e.EventType).ToList();
        Assert.Contains("category.created", types);
        Assert.Contains("category.updated", types);
        Assert.Contains("category.deactivated", types);
    }

    private async Task<IReadOnlyList<CategoryNode>> GetSystemAsync(string qs = "")
        => (await _client.GetFromJsonAsync<CategoryEnvelope>($"{SystemUrl}{qs}"))!.Data;

    private async Task<IReadOnlyList<CategoryNode>> GetUserAsync(string qs = "")
        => (await _client.GetFromJsonAsync<CategoryEnvelope>($"{UserUrl}{qs}"))!.Data;

    private async Task<(HttpStatusCode status, CategoryNode? dto)> CreateAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(UserUrl, body);
        if (response.StatusCode != HttpStatusCode.OK)
            return (response.StatusCode, null);
        var envelope = await response.Content.ReadFromJsonAsync<SingleEnvelope>();
        return (response.StatusCode, envelope!.Data);
    }

    private sealed record CategoryEnvelope(IReadOnlyList<CategoryNode> Data);
    private sealed record SingleEnvelope(CategoryNode Data);
    private sealed record CategoryNode(
        Guid Id, string? Code, string Name, string Nature, Guid? ParentCategoryId,
        string? Color, string? Icon, int DisplayOrder, bool IsOther, bool IsActive,
        IReadOnlyList<CategoryNode> Children);

    private sealed record AuditEnvelope(IReadOnlyList<AuditNode> Data);
    private sealed record AuditNode(string EntityType, Guid EntityId, string EventType);
}
