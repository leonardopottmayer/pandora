using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;
using Pottmayer.Tars.Security.Identity.Abstractions.Services;
using Pottmayer.Tars.Security.Identity.Abstractions.Token;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IUnitOfWorkFactory factory,
    IRefreshTokenService refreshTokenService,
    ITokenIssuer tokenIssuer)
    : CommandHandlerBase<RefreshTokenCommand, TokenDto>
{
    protected override async Task<Result<TokenDto>> HandleAsync(RefreshTokenCommand request, CancellationToken ct)
    {
        // 1. Check for reuse before consuming
        var reuseSubject = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            return await repo.TryGetSubjectForReuseAsync(request.Input.RefreshToken, token);
        }, cancellationToken: ct);

        if (reuseSubject is not null)
        {
            await refreshTokenService.RevokeAsync(request.Input.RefreshToken, ct);
            return Fail(IdentityErrors.TokenReuseDetected);
        }

        // 2. Consume the refresh token
        var consumed = await refreshTokenService.ConsumeAsync(request.Input.RefreshToken, ct);

        if (consumed is null)
            return Fail(IdentityErrors.InvalidRefreshToken);

        // 3. Issue new access token
        var authResult = new AuthenticationResult
        {
            Subject = consumed.Payload.Subject,
            Claims  = consumed.Payload.Claims
        };

        var accessToken = await tokenIssuer.IssueAsync(authResult, ct);

        // 4. Issue new refresh token if rotation is enabled
        string newRefreshToken;
        DateTimeOffset newRefreshExpiresAt;

        if (consumed.ShouldIssueNewRefreshToken)
        {
            var newRefresh = await refreshTokenService.IssueAsync(
                consumed.Payload.Subject, consumed.Payload.Claims, consumed.Payload.Metadata, ct);
            newRefreshToken     = newRefresh.OpaqueToken;
            newRefreshExpiresAt = newRefresh.ExpiresAt;
        }
        else
        {
            newRefreshToken     = request.Input.RefreshToken;
            newRefreshExpiresAt = DateTimeOffset.UtcNow;
        }

        return Ok(new TokenDto(
            AccessToken:           accessToken.AccessToken,
            AccessTokenExpiresAt:  accessToken.ExpiresAt,
            RefreshToken:          newRefreshToken,
            RefreshTokenExpiresAt: newRefreshExpiresAt));
    }
}
