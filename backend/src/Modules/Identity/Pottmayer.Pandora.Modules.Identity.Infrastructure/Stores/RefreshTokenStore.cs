using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;
using Pottmayer.Tars.Security.Identity.Abstractions.Stores;

namespace Pottmayer.Pandora.Modules.Identity.Infrastructure.Stores;

/// <summary>
/// Implements <see cref="IRefreshTokenStore"/> using the relational database.
/// Called internally by Tars' <see cref="Pottmayer.Tars.Security.Identity.Abstractions.Services.IRefreshTokenService"/>.
/// </summary>
internal sealed class RefreshTokenStore(IUnitOfWorkFactory factory) : IRefreshTokenStore
{
    public async ValueTask StoreAsync(
        string tokenId,
        string tokenHash,
        string subject,
        IReadOnlyList<ClaimData> claims,
        DateTimeOffset expiresAt,
        IReadOnlyDictionary<string, object?>? metadata,
        CancellationToken cancellationToken = default)
    {
        await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            await repo.StoreAsync(tokenId, tokenHash, subject, claims, expiresAt, metadata, ct);
        }, cancellationToken: cancellationToken);
    }

    public async ValueTask<RefreshTokenPayload?> GetAndRemoveAsync(
        string tokenId, string tokenHash, CancellationToken cancellationToken = default)
    {
        return await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            return await repo.GetAndRemoveAsync(tokenId, tokenHash, ct);
        }, cancellationToken: cancellationToken);
    }

    public async ValueTask<RefreshTokenPayload?> GetAsync(
        string tokenId, string tokenHash, CancellationToken cancellationToken = default)
    {
        return await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            return await repo.GetAsync(tokenId, tokenHash, ct);
        }, cancellationToken: cancellationToken);
    }

    public async ValueTask RevokeAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            await repo.RevokeAsync(tokenId, ct);
        }, cancellationToken: cancellationToken);
    }

    public async ValueTask RevokeAllForSubjectAsync(
        string subject, CancellationToken cancellationToken = default)
    {
        await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            await repo.RevokeAllForSubjectAsync(subject, ct);
        }, cancellationToken: cancellationToken);
    }

    public async ValueTask<string?> TryGetSubjectForReuseAsync(
        string tokenId, CancellationToken cancellationToken = default)
    {
        return await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            return await repo.TryGetSubjectForReuseAsync(tokenId, ct);
        }, cancellationToken: cancellationToken);
    }
}
