using Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class ActivateAccountCommandHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeIntegrationEventBus _eventBus = new();

    private (ActivateAccountCommandHandler Handler, FakeUserRepository Users, FakeActivationTokenRepository Tokens) Build(
        User user, params AccountActivationToken[] tokens)
    {
        var users     = new FakeUserRepository(user);
        var tokenRepo = new FakeActivationTokenRepository(tokens);
        var ctx       = new FakeDataContext()
            .Register<IUserRepository>(users)
            .Register<IActivationTokenRepository>(tokenRepo);
        var handler   = new ActivateAccountCommandHandler(new FakeUnitOfWorkFactory(ctx), _eventBus, TimeProvider.System);
        return (handler, users, tokenRepo);
    }

    private static (string Plain, AccountActivationToken Token) IssueToken(Guid userId, TimeSpan ttl)
    {
        var plain = ActivationTokens.Generate();
        var token = AccountActivationToken.Issue(userId, ActivationTokens.Hash(plain), DateTimeOffset.UtcNow + ttl);
        return (plain, token);
    }

    private static ActivateAccountCommand Command(string token) => new(new ActivateAccountInput(token));

    [Fact]
    public async Task Activates_pending_user_and_consumes_token()
    {
        var user = TestUsers.Unconfirmed(_hasher);
        var (plain, token) = IssueToken(user.Id, TimeSpan.FromHours(1));
        var (handler, users, tokens) = Build(user, token);

        var result = await handler.Handle(Command(plain), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(users.Users[0].CanAuthenticate);
        Assert.NotNull(tokens.Tokens[0].ConsumedAt);

        // Activation proves e-mail ownership: Channels is asked to provision the verified e-mail channel.
        var published = Assert.Single(_eventBus.Published);
        var activated = Assert.IsType<AccountActivated>(published);
        Assert.Equal(user.Id, activated.UserId);
        Assert.Equal(user.Email.Value, activated.Email);
    }

    [Fact]
    public async Task Fails_when_token_is_unknown()
    {
        var user = TestUsers.Unconfirmed(_hasher);
        var (handler, users, _) = Build(user);

        var result = await handler.Handle(Command("does-not-exist"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidActivationToken", result.Errors[0].Code);
        Assert.False(users.Users[0].CanAuthenticate);
        Assert.Empty(_eventBus.Published);
    }

    [Fact]
    public async Task Fails_when_token_is_expired()
    {
        var user = TestUsers.Unconfirmed(_hasher);
        var (plain, token) = IssueToken(user.Id, TimeSpan.FromHours(-1));
        var (handler, users, _) = Build(user, token);

        var result = await handler.Handle(Command(plain), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidActivationToken", result.Errors[0].Code);
        Assert.False(users.Users[0].CanAuthenticate);
    }

    [Fact]
    public async Task Fails_when_token_already_consumed()
    {
        var user = TestUsers.Unconfirmed(_hasher);
        var (plain, token) = IssueToken(user.Id, TimeSpan.FromHours(1));
        token.Consume(DateTimeOffset.UtcNow);
        var (handler, users, _) = Build(user, token);

        var result = await handler.Handle(Command(plain), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidActivationToken", result.Errors[0].Code);
        Assert.False(users.Users[0].CanAuthenticate);
    }

    [Fact]
    public async Task Fails_when_token_is_blank()
    {
        var user = TestUsers.Unconfirmed(_hasher);
        var (handler, _, _) = Build(user);

        var result = await handler.Handle(Command("  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidActivationToken", result.Errors[0].Code);
    }
}
