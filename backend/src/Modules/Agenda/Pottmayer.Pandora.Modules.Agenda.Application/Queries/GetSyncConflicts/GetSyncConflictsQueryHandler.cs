using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetSyncConflicts;

public sealed class GetSyncConflictsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetSyncConflictsQuery, IReadOnlyList<SyncConflictDto>>
{
    /// <summary>The list shows the recent ones; older rows stay in the table.</summary>
    private const int Shown = 50;

    protected override async Task<Result<IReadOnlyList<SyncConflictDto>>> HandleAsync(
        GetSyncConflictsQuery request, CancellationToken cancellationToken)
    {
        var conflicts = await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, ct) =>
            context.AcquireRepository<ISyncConflictRepository>().GetRecentAsync(request.Input.UserId, Shown, ct),
            cancellationToken: cancellationToken);

        IReadOnlyList<SyncConflictDto> dtos = [.. conflicts.Select(c => c.ToDto())];
        return Ok(dtos);
    }
}
