using System.Text.Json;
using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Application.Services;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Errors;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.FeatureFlags.Abstractions;
using Pottmayer.Tars.Security.Authorization;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.RevokeUserSessions;

public sealed class RevokeUserSessionsCommandHandler(IUnitOfWorkFactory factory, IUserDirectory directory, TimeProvider timeProvider)
    : CommandHandlerBase<RevokeUserSessionsCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(RevokeUserSessionsCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!await directory.RevokeSessionsAsync(input.TargetUserId, ct))
            return Fail(AdminErrors.UserNotFound);

        await factory.ExecuteAsync(AdminModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                input.UserId, AuditEntry.SessionsRevoked, AuditEntry.UserTarget, input.TargetUserId.ToString(),
                before: null, after: null, timeProvider), token),
            cancellationToken: ct);
        return Ok(true);
    }
}
