using Pottmayer.Tars.Security.Authorization;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Services;

/// <summary>What the panel accepts as a grant: <c>*</c>, or an exact key or prefix wildcard that covers a declared permission.</summary>
public static class PatternRules
{
    public static bool IsValid(string pattern, IPermissionCatalog catalog) =>
        pattern == "*" || catalog.Definitions.Any(d => PermissionPatterns.Matches(pattern, d.Key));

    /// <summary>The first pattern that is not valid, or <c>null</c>.</summary>
    public static string? FirstInvalid(IEnumerable<string> patterns, IPermissionCatalog catalog) =>
        patterns.FirstOrDefault(p => !IsValid(p, catalog));
}
