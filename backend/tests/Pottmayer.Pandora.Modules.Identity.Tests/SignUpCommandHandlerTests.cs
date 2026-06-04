using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignUp;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class SignUpCommandHandlerTests
{
    private const string Password = "correct horse battery staple";

    private readonly FakePasswordHasher _hasher = new();

    private (SignUpCommandHandler Handler, FakeUserRepository Users) Build(params User[] existingUsers)
    {
        var users   = new FakeUserRepository(existingUsers);
        var ctx     = new FakeDataContext().Register<IUserRepository>(users);
        var handler = new SignUpCommandHandler(new FakeUnitOfWorkFactory(ctx), _hasher, TimeProvider.System);
        return (handler, users);
    }

    private static SignUpCommand Command(string email = "alice@example.com", string username = "alice", string password = Password)
        => new(new SignUpInput("Alice", username, email, password));

    [Fact]
    public async Task Creates_a_user_with_hashed_password()
    {
        var (handler, users) = Build();

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var user = Assert.Single(users.Users);
        Assert.Equal(user.Id, result.Value!.UserId);
        Assert.True(user.VerifyPassword(Password, _hasher));
        Assert.True(user.CanAuthenticate);
    }

    [Fact]
    public async Task Fails_when_password_is_missing_and_creates_nothing()
    {
        var (handler, users) = Build();

        var result = await handler.Handle(Command(password: "  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.PasswordRequired", result.Errors[0].Code);
        Assert.Empty(users.Users);
    }

    [Fact]
    public async Task Fails_when_email_already_registered()
    {
        var existing = TestUsers.Active(_hasher, "x", username: "bob", email: "alice@example.com");
        var (handler, users) = Build(existing);

        var result = await handler.Handle(Command(username: "newname"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailOrUsernameAlreadyRegistered", result.Errors[0].Code);
        Assert.Single(users.Users); // nothing new added
    }

    [Fact]
    public async Task Fails_when_username_already_taken()
    {
        var existing = TestUsers.Active(_hasher, "x", username: "alice", email: "bob@example.com");
        var (handler, users) = Build(existing);

        var result = await handler.Handle(Command(email: "new@example.com"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailOrUsernameAlreadyRegistered", result.Errors[0].Code);
        Assert.Single(users.Users);
    }
}
