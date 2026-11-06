using Pottmayer.Pandora.Modules.Admin.Application.Dtos;

namespace Pottmayer.Pandora.Modules.Admin.Presentation.Requests;

public sealed record SetFeatureRequest(bool Enabled);

/// <summary>Body of creating or editing a role.</summary>
public sealed record RoleRequest(string Name, string? Description, IReadOnlyList<string>? Patterns);

/// <param name="Assign"><c>true</c> gives the role, <c>false</c> takes it away.</param>
public sealed record ChangeUserRolesRequest(IReadOnlyList<Guid> UserIds, Guid RoleId, bool Assign);

public sealed record SetUserExceptionsRequest(IReadOnlyList<UserExceptionDto>? Exceptions);

public sealed record SetUserDisabledRequest(bool Disabled);
