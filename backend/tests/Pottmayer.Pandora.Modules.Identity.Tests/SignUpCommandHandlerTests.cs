using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignUp;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Contracts;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class SignUpCommandHandlerTests
{
    private const string Password = "Str0ng!Pass";

    private readonly FakePasswordHasher _hasher = new();

    private readonly FakeIntegrationEventBus _eventBus = new();

    private (SignUpCommandHandler Handler, FakeUserRepository Users, FakeActivationTokenRepository Tokens) Build(params User[] existingUsers)
    {
        var users   = new FakeUserRepository(existingUsers);
        var tokens  = new FakeActivationTokenRepository();
        var ctx     = new FakeDataContext()
            .Register<IUserRepository>(users)
            .Register<IActivationTokenRepository>(tokens);
        var options = Options.Create(new AccountActivationOptions());
        var handler = new SignUpCommandHandler(new FakeUnitOfWorkFactory(ctx), _hasher, _eventBus, options, TimeProvider.System);
        return (handler, users, tokens);
    }

    private static SignUpCommand Command(string email = "alice@example.com", string username = "alice", string password = Password)
        => new(new SignUpInput("Alice", username, email, password));

    [Fact]
    public async Task Creates_a_pending_user_with_hashed_password_and_an_activation_token()
    {
        var (handler, users, tokens) = Build();

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var user = Assert.Single(users.Users);
        Assert.Equal(user.Id, result.Value!.UserId);
        Assert.True(user.VerifyPassword(Password, _hasher));
        Assert.False(user.CanAuthenticate); // pending activation

        var published = Assert.Single(_eventBus.Published);
        var activation = Assert.IsType<AccountActivationRequested>(published);
        Assert.Equal(user.Id, activation.UserId);
        Assert.Equal("alice@example.com", activation.Email);
        Assert.False(string.IsNullOrWhiteSpace(activation.Token));

        // The persisted token stores the hash of the plaintext that went out in the e-mail.
        var stored = Assert.Single(tokens.Tokens);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(ActivationTokens.Hash(activation.Token), stored.TokenHash);
        Assert.Null(stored.ConsumedAt);
    }

    [Fact]
    public async Task Fails_when_password_is_missing_and_creates_nothing()
    {
        var (handler, users, _) = Build();

        var result = await handler.Handle(Command(password: "  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.PasswordRequired", result.Errors[0].Code);
        Assert.Empty(users.Users);
    }

    [Fact]
    public async Task Fails_when_password_is_weak_and_creates_nothing()
    {
        var (handler, users, _) = Build();

        var result = await handler.Handle(Command(password: "pandora"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.WeakPassword", result.Errors[0].Code);
        Assert.Empty(users.Users);
    }

    [Fact]
    public async Task Fails_when_email_already_registered()
    {
        var existing = TestUsers.Active(_hasher, "x", username: "bob", email: "alice@example.com");
        var (handler, users, _) = Build(existing);

        var result = await handler.Handle(Command(username: "newname"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailOrUsernameAlreadyRegistered", result.Errors[0].Code);
        Assert.Single(users.Users); // nothing new added
    }

    [Fact]
    public async Task Fails_when_username_already_taken()
    {
        var existing = TestUsers.Active(_hasher, "x", username: "alice", email: "bob@example.com");
        var (handler, users, _) = Build(existing);

        var result = await handler.Handle(Command(email: "new@example.com"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailOrUsernameAlreadyRegistered", result.Errors[0].Code);
        Assert.Single(users.Users);
    }
}
