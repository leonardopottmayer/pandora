using Pottmayer.Pandora.Modules.Identity.Application.Commands.RefreshToken;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;
using Pottmayer.Tars.Security.Identity.Abstractions.Stores;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class RefreshTokenCommandHandlerTests
{
    private static RefreshTokenCommandHandler Build(
        FakeRefreshTokenService refresh, FakeRefreshTokenRepository repo)
    {
        var ctx     = new FakeDataContext().Register<IRefreshTokenRepository>(repo);
        var factory = new FakeUnitOfWorkFactory(ctx);
        return new RefreshTokenCommandHandler(factory, refresh, new FakeTokenIssuer());
    }

    [Fact]
    public async Task Rotates_tokens_on_valid_refresh()
    {
        var refresh = new FakeRefreshTokenService
        {
            NextConsume = new RefreshTokenConsumeResult(
                new RefreshTokenPayload { Subject = "sub", Claims = [] },
                ShouldIssueNewRefreshToken: true)
        };
        var handler = Build(refresh, new FakeRefreshTokenRepository());

        var result = await handler.Handle(
            new RefreshTokenCommand(new RefreshTokenInput("opaque")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value!.AccessToken);
        Assert.Equal("refresh-token", result.Value!.RefreshToken);
        Assert.Equal(1, refresh.IssueCount);
    }

    [Fact]
    public async Task Fails_when_refresh_token_is_invalid()
    {
        var refresh = new FakeRefreshTokenService { NextConsume = null };
        var handler = Build(refresh, new FakeRefreshTokenRepository());

        var result = await handler.Handle(
            new RefreshTokenCommand(new RefreshTokenInput("opaque")), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidRefreshToken", result.Errors[0].Code);
    }

    [Fact]
    public async Task Revokes_and_fails_on_reuse_detection()
    {
        var refresh = new FakeRefreshTokenService();
        var handler = Build(refresh, new FakeRefreshTokenRepository(reuseSubject: "sub"));

        var result = await handler.Handle(
            new RefreshTokenCommand(new RefreshTokenInput("opaque")), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.TokenReuseDetected", result.Errors[0].Code);
    }
}
