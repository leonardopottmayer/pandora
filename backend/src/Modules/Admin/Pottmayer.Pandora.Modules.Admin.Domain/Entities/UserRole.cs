namespace Pottmayer.Pandora.Modules.Admin.Domain.Entities;

/// <summary>A role held by a user. The user lives in Identity; the id is a logical reference.</summary>
public sealed class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    private UserRole() { }

    public static UserRole Create(Guid userId, Guid roleId) => new() { UserId = userId, RoleId = roleId };
}
