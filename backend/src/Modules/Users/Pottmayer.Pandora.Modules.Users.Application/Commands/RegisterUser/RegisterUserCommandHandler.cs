using Pottmayer.Pandora.Modules.Users.Abstractions;
using Pottmayer.Pandora.Modules.Users.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Users.Domain.Errors;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Services;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Users.Application.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : CommandHandlerBase<RegisterUserCommand, RegisterUserResult>
{
    protected override async Task<Result<RegisterUserResult>> HandleAsync(
        RegisterUserCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!Email.TryCreate(input.Email, out var email))
            return Fail(UserErrors.InvalidEmail);

        if (string.IsNullOrWhiteSpace(input.Username))
            return Fail(UserErrors.InvalidUsername);

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(UserErrors.InvalidName);

        if (string.IsNullOrWhiteSpace(input.Password))
            return Fail(UserErrors.InvalidPassword);

        return await factory.ExecuteAsync(UsersModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();

            var byEmail    = await repo.FindByEmailAsync(email!, token);
            var byUsername = await repo.FindByUsernameAsync(input.Username.Trim(), token);

            if (byEmail is not null)    return Fail(UserErrors.EmailAlreadyRegistered);
            if (byUsername is not null) return Fail(UserErrors.UsernameAlreadyRegistered);

            var hashed = passwordHasher.Hash(input.Password);
            var user   = User.Register(input.Name, input.Username, email!, hashed, timeProvider);

            await repo.AddAsync(user, token);

            return Ok(new RegisterUserResult(user.Id));
        }, cancellationToken: ct);
    }
}
