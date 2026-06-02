using Pottmayer.Pandora.Modules.Users.Abstractions;
using Pottmayer.Pandora.Modules.Users.Contracts.Authentication;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Services;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Users.Application.Services;

internal sealed class UserAuthenticatorService(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher) : IUserAuthenticator
{
    public async Task<UserAuthResult> AuthenticateAsync(
        string emailOrUsername, string password, CancellationToken ct = default)
    {
        var user = await factory.ExecuteAsync(UsersModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();

            if (Email.TryCreate(emailOrUsername, out var email))
                return await repo.FindByEmailAsync(email!, token);

            return await repo.FindByUsernameAsync(emailOrUsername, token);
        }, cancellationToken: ct);

        if (user is null)
            return UserAuthResult.InvalidCredentials();

        if (!user.IsActive)
            return UserAuthResult.AccountNotActive();

        if (!user.VerifyPassword(password, passwordHasher))
            return UserAuthResult.InvalidCredentials();

        return UserAuthResult.Success(new UserAuthDto(user.Id, user.Username, user.Email.Value, user.Name));
    }
}
