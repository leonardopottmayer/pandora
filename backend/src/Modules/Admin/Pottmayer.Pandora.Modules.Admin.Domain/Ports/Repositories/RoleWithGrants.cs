using Pottmayer.Pandora.Modules.Admin.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

/// <summary>A role with its patterns and the number of users holding it.</summary>
public sealed record RoleWithGrants(Role Role, IReadOnlyList<string> Patterns, int Holders);
