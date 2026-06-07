using Pottmayer.Pandora.Modules.Identity.Application.Commands.PasswordReset;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class ResetPasswordCommandHandlerTests
{
    private const string NewPassword = "Str0ng!Pass";

    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeIntegrationEventBus _eventBus = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();

    private (ResetPasswordCommandHandler Handler, FakeUserRepository Users, FakePasswordResetTokenRepository Tokens) Build(
        User user, params PasswordResetToken[] tokens)
    {
        var users     = new FakeUserRepository(user);
        var tokenRepo = new FakePasswordResetTokenRepository(tokens);
        var ctx       = new FakeDataContext()
            .Register<IUserRepository>(users)
            .Register<IPasswordResetTokenRepository>(tokenRepo)
            .Register<IRefreshTokenRepository>(_refreshTokens);
        var handler   = new ResetPasswordCommandHandler(
            new FakeUnitOfWorkFactory(ctx), _hasher, _eventBus, TimeProvider.System);
        return (handler, users, tokenRepo);
    }

    private static (string Plain, PasswordResetToken Token) IssueToken(Guid userId, TimeSpan ttl)
    {
        var plain = PasswordResetTokens.Generate();
        var token = PasswordResetToken.Issue(userId, PasswordResetTokens.Hash(plain), DateTimeOffset.UtcNow + ttl);
        return (plain, token);
    }

    private static ResetPasswordCommand Command(string token, string newPassword = NewPassword)
        => new(new ResetPasswordInput(token, newPassword));

    [Fact]
    public async Task Resets_password_consumes_token_revokes_sessions_and_notifies()
    {
        var user = TestUsers.Active(_hasher, "OldP4ss!word");
        var (plain, token) = IssueToken(user.Id, TimeSpan.FromHours(1));
        var (handler, users, tokens) = Build(user, token);

        var result = await handler.Handle(Command(plain), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(users.Users[0].VerifyPassword(NewPassword, _hasher));
        Assert.NotNull(tokens.Tokens[0].ConsumedAt);
        Assert.Equal(user.Id.ToString(), Assert.Single(_refreshTokens.RevokedSubjects));
        Assert.IsType<PasswordChanged>(Assert.Single(_eventBus.Published));
    }

    [Fact]
    public async Task Fails_when_token_is_unknown()
    {
        var user = TestUsers.Active(_hasher);
        var (handler, _, _) = Build(user);

        var result = await handler.Handle(Command("does-not-exist"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidPasswordResetToken", result.Errors[0].Code);
        Assert.Empty(_refreshTokens.RevokedSubjects);
    }

    [Fact]
    public async Task Fails_when_token_is_expired()
    {
        var user = TestUsers.Active(_hasher);
        var (plain, token) = IssueToken(user.Id, TimeSpan.FromHours(-1));
        var (handler, _, _) = Build(user, token);

        var result = await handler.Handle(Command(plain), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidPasswordResetToken", result.Errors[0].Code);
    }

    [Fact]
    public async Task Fails_when_token_already_consumed()
    {
        var user = TestUsers.Active(_hasher);
        var (plain, token) = IssueToken(user.Id, TimeSpan.FromHours(1));
        token.Consume(DateTimeOffset.UtcNow);
        var (handler, _, _) = Build(user, token);

        var result = await handler.Handle(Command(plain), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidPasswordResetToken", result.Errors[0].Code);
    }

    [Fact]
    public async Task Fails_when_new_password_violates_policy()
    {
        var user = TestUsers.Active(_hasher);
        var (plain, token) = IssueToken(user.Id, TimeSpan.FromHours(1));
        var (handler, _, tokens) = Build(user, token);

        var result = await handler.Handle(Command(plain, "weak"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.WeakPassword", result.Errors[0].Code);
        Assert.Null(tokens.Tokens[0].ConsumedAt); // token untouched
        Assert.Empty(_refreshTokens.RevokedSubjects);
    }
}
