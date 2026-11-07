using System.Globalization;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Application.Users;

/// <summary>Finds, lists and disables accounts for other modules. No aggregate crosses this boundary.</summary>
public sealed class UserDirectory(IUnitOfWorkFactory factory, IIntegrationEventBus integrationEventBus, TimeProvider timeProvider)
    : IUserDirectory
{
    public async Task<Guid?> FindActivatedUserIdByEmailAsync(string email, CancellationToken ct = default)
    {
        if (!Email.TryCreate(email, out var address))
            return null;

        var user = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();
            return await repo.FindByEmailAsync(address!, token);
        }, cancellationToken: ct);

        return user?.EmailConfirmedAt is null ? null : user.Id;
    }

    // ponytail: every account in one read; page it when an install has thousands of users.
    public async Task<IReadOnlyList<DirectoryUser>> ListAsync(CancellationToken ct = default)
    {
        var users = await factory.ExecuteAsync(IdentityModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IUserRepository>().GetAllAsync(token), cancellationToken: ct);

        return [.. users.OrderByDescending(u => u.CreatedAt).Select(ToDirectoryUser)];
    }

    public async Task<DirectoryUser?> FindAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await factory.ExecuteAsync(IdentityModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IUserRepository>().GetByIdAsync(userId, token), cancellationToken: ct);

        return user is null ? null : ToDirectoryUser(user);
    }

    public Task<bool> SetDisabledAsync(Guid userId, bool disabled, CancellationToken ct = default) =>
        factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(userId, token);
            if (user is null)
                return false;

            if (disabled)
            {
                user.Disable(timeProvider);
                await ctx.AcquireRepository<IRefreshTokenRepository>().RevokeAllForSubjectAsync(user.Id.ToString(), token);
            }
            else
            {
                user.Enable();
            }

            await users.UpdateAsync(user, token);
            return true;
        }, cancellationToken: ct);

    public Task<bool> ApproveAsync(Guid userId, CancellationToken ct = default) =>
        factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.FindByIdWithPreferencesAsync(userId, token);
            if (user is null)
                return false;
            if (!user.IsAwaitingApproval)
                return true;

            user.Approve();
            await users.UpdateAsync(user, token);

            // Published inside the unit of work: the e-mail goes out only if the approval commits.
            await integrationEventBus.PublishAsync(new AccountApproved(
                EventId: Guid.CreateVersion7(),
                OccurredAt: timeProvider.GetUtcNow(),
                UserId: user.Id,
                Email: user.Email.Value,
                Locale: user.Preferences?.Language.Value ?? CultureInfo.CurrentUICulture.Name), token);
            return true;
        }, cancellationToken: ct);

    public Task<bool> RevokeSessionsAsync(Guid userId, CancellationToken ct = default) =>
        factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            if (!await ctx.AcquireRepository<IUserRepository>().ExistsKeyAsync(userId, token))
                return false;

            await ctx.AcquireRepository<IRefreshTokenRepository>().RevokeAllForSubjectAsync(userId.ToString(), token);
            return true;
        }, cancellationToken: ct);

    private static DirectoryUser ToDirectoryUser(User user) => new(
        user.Id, user.Name, user.Username, user.Email.Value,
        Activated: user.EmailConfirmedAt is not null,
        Disabled: user.DisabledAt is not null,
        AwaitingApproval: user.IsAwaitingApproval,
        user.MfaEnabled, user.LastSignInAt, user.CreatedAt);
}
