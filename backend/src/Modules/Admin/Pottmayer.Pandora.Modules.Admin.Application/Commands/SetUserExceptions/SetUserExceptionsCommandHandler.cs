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

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.SetUserExceptions;

public sealed class SetUserExceptionsCommandHandler(
    IUnitOfWorkFactory factory, IUserDirectory directory, IPermissionCatalog catalog, IPermissionChecker checker,
    TimeProvider timeProvider)
    : CommandHandlerBase<SetUserExceptionsCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(SetUserExceptionsCommand request, CancellationToken ct)
    {
        var input = request.Input;
        // One effect per pattern: the last one sent wins.
        var wanted = input.Exceptions
            .GroupBy(e => e.Pattern.Trim())
            .Select(g => new UserExceptionDto(g.Key, g.Last().Effect))
            .ToList();
        if (wanted.Any(e => e.Effect is not (UserPermission.Allow or UserPermission.Deny)))
            return Fail(AdminErrors.InvalidEffect);
        if (PatternRules.FirstInvalid(wanted.Select(e => e.Pattern), catalog) is { } invalid)
            return Fail(AdminErrors.InvalidPattern(invalid));
        if (await directory.FindAsync(input.TargetUserId, ct) is null)
            return Fail(AdminErrors.UserNotFound);

        await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserPermissionRepository>();
            var current = (await repo.GetAsync(p => p.UserId == input.TargetUserId, token)).ToList();
            var before = JsonSerializer.Serialize(current.OrderBy(p => p.Pattern, StringComparer.Ordinal).Select(p => new { pattern = p.Pattern, effect = p.Effect }));

            await repo.RemoveRangeAsync(current, token);
            await repo.AddRangeAsync(wanted.Select(e => UserPermission.Create(input.TargetUserId, e.Pattern, e.Effect)), token);

            await ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                input.UserId, AuditEntry.ExceptionsChanged, AuditEntry.UserTarget, input.TargetUserId.ToString(), before,
                JsonSerializer.Serialize(wanted.OrderBy(e => e.Pattern, StringComparer.Ordinal).Select(e => new { pattern = e.Pattern, effect = e.Effect })),
                timeProvider), token);
            return true;
        }, cancellationToken: ct);

        checker.Invalidate(input.TargetUserId.ToString());
        return Ok(true);
    }
}
