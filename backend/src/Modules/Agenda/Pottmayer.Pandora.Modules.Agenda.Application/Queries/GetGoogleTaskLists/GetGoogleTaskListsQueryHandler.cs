using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetGoogleTaskLists;

public sealed class GetGoogleTaskListsQueryHandler(IUnitOfWorkFactory factory, IGoogleTasksClient google)
    : QueryHandlerBase<GetGoogleTaskListsQuery, IReadOnlyList<GoogleTaskListDto>>
{
    protected override async Task<Result<IReadOnlyList<GoogleTaskListDto>>> HandleAsync(
        GetGoogleTaskListsQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var remote = await google.ListTaskListsAsync(input.UserId, input.AccountId, cancellationToken);
        if (!remote.IsSuccess)
            return Fail(remote.Errors);

        var bound = await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, ct) =>
            context.AcquireRepository<ITaskListBindingRepository>().GetByAccountAsync(input.AccountId, ct),
            cancellationToken: cancellationToken);
        var bindingOf = bound.Where(b => b.UserId == input.UserId).ToDictionary(b => b.RemoteListId, b => b.Id);

        IReadOnlyList<GoogleTaskListDto> dtos =
        [
            .. remote.Value!
                .Select(l => new GoogleTaskListDto(
                    l.Id, string.IsNullOrWhiteSpace(l.Title) ? l.Id : l.Title,
                    bindingOf.TryGetValue(l.Id, out var bindingId) ? bindingId : null))
                .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
        ];
        return Ok(dtos);
    }
}
