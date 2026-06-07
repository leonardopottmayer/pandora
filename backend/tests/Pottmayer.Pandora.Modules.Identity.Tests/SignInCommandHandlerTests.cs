using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class SignInCommandHandlerTests
{
    private const string Password = "correct horse battery staple";

    private readonly FakePasswordHasher _hasher = new();

    private (SignInCommandHandler Handler, FakeUserRepository Users, FakeRefreshTokenService Refresh) Build(
        FakeUserRepository users)
    {
        var refresh = new FakeRefreshTokenService();
        var ctx     = new FakeDataContext().Register<IUserRepository>(users);
        var handler = new SignInCommandHandler(
            new FakeUnitOfWorkFactory(ctx), _hasher, new FakeTokenIssuer(), refresh,
            Options.Create(new MfaOptions()), TimeProvider.System);
        return (handler, users, refresh);
    }

    private static SignInCommand Command(string emailOrUsername, string password = Password)
        => new(new SignInInput(emailOrUsername, password));

    [Fact]
    public async Task Issues_tokens_when_signing_in_by_email()
    {
        var (handler, _, refresh) = Build(new FakeUserRepository(TestUsers.Active(_hasher, Password)));

        var result = await handler.Handle(Command("alice@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value!.Tokens!.AccessToken);
        Assert.Equal("refresh-token", result.Value!.Tokens!.RefreshToken);
        Assert.Equal(1, refresh.IssueCount);
    }

    [Fact]
    public async Task Issues_tokens_when_signing_in_by_username()
    {
        var (handler, _, _) = Build(new FakeUserRepository(TestUsers.Active(_hasher, Password)));

        var result = await handler.Handle(Command("alice"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Records_sign_in_on_success()
    {
        var (handler, users, _) = Build(new FakeUserRepository(TestUsers.Active(_hasher, Password)));

        await handler.Handle(Command("alice"), CancellationToken.None);

        Assert.NotNull(users.Users[0].LastSignInAt);
    }

    [Fact]
    public async Task Fails_with_invalid_credentials_on_wrong_password()
    {
        var (handler, _, refresh) = Build(new FakeUserRepository(TestUsers.Active(_hasher, Password)));

        var result = await handler.Handle(Command("alice", "wrong"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidCredentials", result.Errors[0].Code);
        Assert.Equal(0, refresh.IssueCount);
    }

    [Fact]
    public async Task Fails_with_invalid_credentials_when_user_not_found()
    {
        var (handler, _, _) = Build(new FakeUserRepository());

        var result = await handler.Handle(Command("ghost@example.com"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidCredentials", result.Errors[0].Code);
    }

    [Fact]
    public async Task Fails_with_account_not_active_when_status_disallows()
    {
        var (handler, _, _) = Build(new FakeUserRepository(TestUsers.Disabled(_hasher, Password)));

        var result = await handler.Handle(Command("alice"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.AccountNotActive", result.Errors[0].Code);
    }
}
