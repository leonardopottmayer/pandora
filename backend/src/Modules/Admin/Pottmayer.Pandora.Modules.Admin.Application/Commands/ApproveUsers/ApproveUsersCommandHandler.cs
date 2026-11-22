using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Errors;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.ApproveUsers;

/// <summary>Identity approves each account (and e-mails the user); the audit entries are written here after.</summary>
public sealed class ApproveUsersCommandHandler(IUnitOfWorkFactory factory, IUserDirectory directory, TimeProvider timeProvider)
    : CommandHandlerBase<ApproveUsersCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(ApproveUsersCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var users = (await directory.ListAsync(ct)).ToDictionary(u => u.Id);
        var targets = input.UserIds.Distinct().ToList();
        if (targets.Any(id => !users.ContainsKey(id)))
            return Fail(AdminErrors.UserNotFound);

        var waiting = targets.Where(id => users[id].AwaitingApproval).ToList();
        foreach (var id in waiting)
            await directory.ApproveAsync(id, ct);

        await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var audits = ctx.AcquireRepository<IAuditEntryRepository>();
            foreach (var id in waiting)
            {
                await audits.AddAsync(AuditEntry.Record(
                    input.UserId, AuditEntry.UserApproved, AuditEntry.UserTarget, id.ToString(),
                    before: null, after: null, timeProvider), token);
            }
            return true;
        }, cancellationToken: ct);

        return Ok(waiting.Count);
    }
}
