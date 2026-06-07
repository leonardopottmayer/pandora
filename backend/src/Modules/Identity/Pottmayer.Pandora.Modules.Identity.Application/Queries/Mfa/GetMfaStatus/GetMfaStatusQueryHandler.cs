using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.Mfa.GetMfaStatus;

public sealed class GetMfaStatusQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetMfaStatusQuery, MfaStatusDto>
{
    protected override async Task<Result<MfaStatusDto>> HandleAsync(
        GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        return await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var user = await ctx.AcquireRepository<IUserRepository>().GetByIdAsync(request.Input.UserId, ct);
            if (user is null)
                return Fail(UserErrors.NotFound);

            var remaining = user.MfaEnabled
                ? await ctx.AcquireRepository<IMfaRecoveryCodeRepository>().CountActiveByUserIdAsync(user.Id, ct)
                : 0;

            return Ok(new MfaStatusDto(user.MfaEnabled, remaining));
        }, cancellationToken: cancellationToken);
    }
}
