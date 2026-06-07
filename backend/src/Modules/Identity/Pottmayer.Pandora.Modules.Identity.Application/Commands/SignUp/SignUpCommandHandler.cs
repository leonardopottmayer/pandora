using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Security;
using Pottmayer.Pandora.Modules.Identity.Contracts;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;
using System.Globalization;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignUp;

public sealed class SignUpCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    IIntegrationEventBus integrationEventBus,
    IOptions<AccountActivationOptions> activationOptions,
    TimeProvider timeProvider)
    : CommandHandlerBase<SignUpCommand, SignUpResult>
{
    protected override async Task<Result<SignUpResult>> HandleAsync(SignUpCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Password))
            return Fail(IdentityErrors.PasswordRequired);

        if (!PasswordPolicy.IsSatisfiedBy(input.Password))
            return Fail(IdentityErrors.WeakPassword);

        if (!Email.TryCreate(input.Email, out var email))
            return Fail(UserErrors.InvalidEmail);

        if (string.IsNullOrWhiteSpace(input.Username))
            return Fail(UserErrors.InvalidUsername);

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(UserErrors.InvalidName);

        var passwordHash = passwordHasher.Hash(input.Password);
        var activationToken = ActivationTokens.Generate();
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();

            if (await users.FindByEmailAsync(email!, token) is not null)
                return Fail(UserErrors.EmailOrUsernameAlreadyRegistered);

            if (await users.FindByUsernameAsync(input.Username.Trim(), token) is not null)
                return Fail(UserErrors.EmailOrUsernameAlreadyRegistered);

            // The account starts pending: User.Register leaves EmailConfirmedAt null, so it can't sign in yet.
            var user = User.Register(input.Name, input.Username, email!, passwordHash, timeProvider);
            await users.AddAsync(user, token);

            var activation = AccountActivationToken.Issue(
                user.Id,
                ActivationTokens.Hash(activationToken),
                now + activationOptions.Value.TokenLifetime);
            await ctx.AcquireRepository<IActivationTokenRepository>().AddAsync(activation, token);

            return Ok(new SignUpResult(user.Id));
        }, cancellationToken: ct);

        // After commit, ask Notifications to send the activation e-mail (in-process; broker-ready).
        if (result.IsSuccess && result.Value is { } signUp)
        {
            var activationRequested = new AccountActivationRequested(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                UserId: signUp.UserId,
                Email: email!.Value,
                Token: activationToken,
                Locale: CultureInfo.CurrentUICulture.Name);

            await integrationEventBus.PublishAsync(activationRequested, ct);
        }

        return result;
    }
}
