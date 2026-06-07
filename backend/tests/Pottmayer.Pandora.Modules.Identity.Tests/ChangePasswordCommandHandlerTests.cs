using Pottmayer.Pandora.Modules.Identity.Application.Commands.ChangePassword;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class ChangePasswordCommandHandlerTests
{
    private const string CurrentPassword = "Curr3nt!Pass";
    private const string NewPassword = "Str0ng!Pass";

    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeIntegrationEventBus _eventBus = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();

    private (ChangePasswordCommandHandler Handler, FakeUserRepository Users) Build(User user)
    {
        var users   = new FakeUserRepository(user);
        var ctx     = new FakeDataContext()
            .Register<IUserRepository>(users)
            .Register<IRefreshTokenRepository>(_refreshTokens);
        var handler = new ChangePasswordCommandHandler(
            new FakeUnitOfWorkFactory(ctx), _hasher, _eventBus, TimeProvider.System);
        return (handler, users);
    }

    private static ChangePasswordCommand Command(Guid userId, string current = CurrentPassword, string @new = NewPassword)
        => new(new ChangePasswordInput(userId, current, @new));

    [Fact]
    public async Task Changes_password_revokes_sessions_and_notifies()
    {
        var user = TestUsers.Active(_hasher, CurrentPassword);
        var (handler, users) = Build(user);

        var result = await handler.Handle(Command(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(users.Users[0].VerifyPassword(NewPassword, _hasher));
        Assert.Equal(user.Id.ToString(), Assert.Single(_refreshTokens.RevokedSubjects));
        Assert.IsType<PasswordChanged>(Assert.Single(_eventBus.Published));
    }

    [Fact]
    public async Task Fails_when_current_password_is_wrong()
    {
        var user = TestUsers.Active(_hasher, CurrentPassword);
        var (handler, users) = Build(user);

        var result = await handler.Handle(Command(user.Id, current: "WrongP4ss!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidCredentials", result.Errors[0].Code);
        Assert.True(users.Users[0].VerifyPassword(CurrentPassword, _hasher)); // unchanged
        Assert.Empty(_refreshTokens.RevokedSubjects);
    }

    [Fact]
    public async Task Fails_when_new_password_violates_policy()
    {
        var user = TestUsers.Active(_hasher, CurrentPassword);
        var (handler, _) = Build(user);

        var result = await handler.Handle(Command(user.Id, @new: "weak"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.WeakPassword", result.Errors[0].Code);
        Assert.Empty(_refreshTokens.RevokedSubjects);
    }
}
