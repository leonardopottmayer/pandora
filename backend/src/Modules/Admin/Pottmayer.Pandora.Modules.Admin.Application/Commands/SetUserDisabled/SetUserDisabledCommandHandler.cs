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

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.SetUserDisabled;

/// <summary>Identity changes the account; the audit entry is written here after it.</summary>
public sealed class SetUserDisabledCommandHandler(
    IUnitOfWorkFactory factory, IUserDirectory directory, LastAdminGuard guard, TimeProvider timeProvider)
    : CommandHandlerBase<SetUserDisabledCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(SetUserDisabledCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (await directory.FindAsync(input.TargetUserId, ct) is not { } user)
            return Fail(AdminErrors.UserNotFound);
        if (user.Disabled == input.Disabled)
            return Ok(true);

        if (input.Disabled)
        {
            var admins = await factory.ExecuteAsync(AdminModule.DatabaseKey, (ctx, token) =>
                ctx.AcquireRepository<IUserRoleRepository>().ListHolderIdsAsync(AdminModule.AdminRoleId, token), cancellationToken: ct);
            if (admins.Contains(user.Id) && !await guard.LeavesAnAdminAsync(admins, [user.Id], ct))
                return Fail(AdminErrors.LastAdmin);
        }

        await directory.SetDisabledAsync(user.Id, input.Disabled, ct);
        await factory.ExecuteAsync(AdminModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                input.UserId, input.Disabled ? AuditEntry.UserDisabled : AuditEntry.UserEnabled, AuditEntry.UserTarget,
                user.Id.ToString(), before: null, after: null, timeProvider), token),
            cancellationToken: ct);
        return Ok(true);
    }
}
