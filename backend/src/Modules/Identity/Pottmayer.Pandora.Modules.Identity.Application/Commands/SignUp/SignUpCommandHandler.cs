using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignUp;

public sealed class SignUpCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : CommandHandlerBase<SignUpCommand, SignUpResult>
{
    protected override async Task<Result<SignUpResult>> HandleAsync(SignUpCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Password))
            return Fail(IdentityErrors.PasswordRequired);

        if (!Email.TryCreate(input.Email, out var email))
            return Fail(UserErrors.InvalidEmail);

        if (string.IsNullOrWhiteSpace(input.Username))
            return Fail(UserErrors.InvalidUsername);

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(UserErrors.InvalidName);

        var passwordHash = passwordHasher.Hash(input.Password);

        return await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();

            if (await users.FindByEmailAsync(email!, token) is not null)
                return Fail(UserErrors.EmailOrUsernameAlreadyRegistered);

            if (await users.FindByUsernameAsync(input.Username.Trim(), token) is not null)
                return Fail(UserErrors.EmailOrUsernameAlreadyRegistered);

            var user = User.Register(input.Name, input.Username, email!, passwordHash, timeProvider);
            await users.AddAsync(user, token);

            return Ok(new SignUpResult(user.Id));
        }, cancellationToken: ct);
    }
}
