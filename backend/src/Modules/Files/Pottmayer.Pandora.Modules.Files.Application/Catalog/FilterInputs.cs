using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Files.Application.Catalog;

/// <summary>A filter as the user edits it. Scope: <see cref="ScopePath"/> needs <see cref="RootId"/>; a root implies its device.</summary>
public sealed record FilterInput(
    Guid? DeviceId,
    Guid? RootId,
    string? ScopePath,
    string? Name,
    string? Action,
    string? AppliesTo,
    string? Matcher,
    string? Pattern,
    bool? CaseSensitive,
    bool IsEnabled);

internal static class FilterInputs
{
    /// <summary>Checks the shape of the filter, then that its root or device is the user's.</summary>
    public static async Task<Result<FilterDefinition>> ValidateAsync(
        FilterInput input, Guid userId, IDataContext ctx, IDeviceReader devices, CancellationToken ct)
    {
        var shape = ValidateShape(input);
        if (shape.IsFailure) return shape;

        var definition = shape.Value!;
        if (definition.RootId is { } rootId)
        {
            var root = await ctx.AcquireRepository<IRootRepository>().FindForUserAsync(rootId, userId, ct);
            if (root is not { IsActive: true }) return FilesErrors.RootNotFound;
        }
        else if (definition.DeviceId is { } deviceId && await devices.GetActiveAsync(userId, deviceId, ct) is null)
        {
            return FilesErrors.DeviceNotFound;
        }

        return Result<FilterDefinition>.Success(definition);
    }

    private static Result<FilterDefinition> ValidateShape(FilterInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > Filter.NameMaxLength) return FilesErrors.InvalidFilterName;
        if (!FilterAction.IsSupported(i.Action)) return FilesErrors.InvalidFilterAction;
        if (!FilterTarget.IsSupported(i.AppliesTo)) return FilesErrors.InvalidFilterTarget;
        if (i.AppliesTo == FilterTarget.Folder.Value && i.Action == FilterAction.Include.Value) return FilesErrors.FolderFilterIncludes;
        if (i.Pattern is { Length: > Filter.PatternMaxLength }) return FilesErrors.InvalidPattern("it is longer than 500 characters.");
        if (NameMatcher.Validate(i.Matcher ?? string.Empty, i.Pattern) is { } reason) return FilesErrors.InvalidPattern(reason);

        string? scopePath = null;
        if (i.ScopePath is not null)
        {
            if (i.RootId is null) return FilesErrors.ScopePathNeedsRoot;
            if (!RootRules.TryNormalize(i.ScopePath, out var path)) return FilesErrors.InvalidPath(i.ScopePath);
            scopePath = path == CatalogPath.Root ? null : path;
        }

        return Result<FilterDefinition>.Success(new FilterDefinition(
            i.RootId is null ? i.DeviceId : null, i.RootId, scopePath, i.Name,
            FilterAction.FromValue(i.Action!), FilterTarget.FromValue(i.AppliesTo!), FilterMatcher.FromValue(i.Matcher!),
            i.Pattern!, i.CaseSensitive, i.IsEnabled));
    }
}
