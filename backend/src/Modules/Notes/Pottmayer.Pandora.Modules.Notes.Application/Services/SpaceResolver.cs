using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>Which space a new page goes to when the caller may or may not have named one.</summary>
internal static class SpaceResolver
{
    /// <summary>
    /// Name for the default space created on the fly. Only an API call without a space meets it — the
    /// web app always names one, and a user's first space becomes the default when they create it.
    /// </summary>
    public const string FallbackName = "Personal";

    /// <summary>
    /// The named space, which must be the user's and not deleted; with none named, the default one,
    /// created when the user has no space at all yet.
    /// </summary>
    public static async Task<Result<Space>> ResolveAsync(
        ISpaceRepository spaces, Guid userId, Guid? spaceId, TimeProvider timeProvider, CancellationToken ct)
    {
        if (spaceId is { } id)
        {
            var named = await spaces.FindByIdForUserAsync(id, userId, ct);
            return named is null ? Result<Space>.Failure([SpaceErrors.NotFound]) : Result<Space>.Success(named);
        }

        var fallback = await spaces.FindDefaultAsync(userId, ct);
        if (fallback is null)
        {
            fallback = Space.Create(userId, FallbackName, icon: null, color: null, description: null,
                isDefault: true, timeProvider);
            await spaces.AddAsync(fallback, ct);
        }

        return Result<Space>.Success(fallback);
    }
}
