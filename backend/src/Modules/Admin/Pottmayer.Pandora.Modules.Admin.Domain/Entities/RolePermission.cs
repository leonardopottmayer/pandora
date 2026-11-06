namespace Pottmayer.Pandora.Modules.Admin.Domain.Entities;

/// <summary>A pattern a role holds: an exact key, a prefix wildcard (<c>finances.*</c>) or <c>*</c>.</summary>
public sealed class RolePermission
{
    public Guid RoleId { get; private set; }
    public string Pattern { get; private set; } = default!;

    private RolePermission() { }

    public static RolePermission Create(Guid roleId, string pattern) => new() { RoleId = roleId, Pattern = pattern };
}
