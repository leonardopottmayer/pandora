using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleTaskLists;

public sealed class BindGoogleTaskListsCommandHandler(
    IUnitOfWorkFactory factory, IGoogleTasksClient google, TimeProvider timeProvider)
    : CommandHandlerBase<BindGoogleTaskListsCommand, IReadOnlyList<TaskListBindingDto>>
{
    protected override async Task<Result<IReadOnlyList<TaskListBindingDto>>> HandleAsync(
        BindGoogleTaskListsCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var wanted = input.RemoteListIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (wanted.Count == 0)
            return Fail(GoogleTasksErrors.NothingToBind);

        // Reading the account's lists proves it is a working Google account with the Tasks scope.
        var remote = await google.ListTaskListsAsync(input.UserId, input.AccountId, ct);
        if (!remote.IsSuccess)
            return Result<IReadOnlyList<TaskListBindingDto>>.Failure(remote.Errors);

        var byId = remote.Value!.ToDictionary(l => l.Id, StringComparer.Ordinal);
        if (wanted.Any(id => !byId.ContainsKey(id)))
            return Fail(GoogleTasksErrors.ListNotFound);

        return await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var lists = context.AcquireRepository<ITaskListRepository>();
            var bindings = context.AcquireRepository<ITaskListBindingRepository>();
            var alreadyBound = (await bindings.GetByAccountAsync(input.AccountId, token))
                .Select(b => b.RemoteListId).ToHashSet(StringComparer.Ordinal);
            var position = (await lists.GetByUserAsync(input.UserId, token)).Count;

            var created = new List<TaskListBindingDto>();
            foreach (var id in wanted.Where(id => !alreadyBound.Contains(id)))
            {
                var name = string.IsNullOrWhiteSpace(byId[id].Title) ? "Google Tasks" : byId[id].Title!;
                var list = TaskList.Create(input.UserId, name, isDefault: false, position++, timeProvider);
                await lists.AddAsync(list, token);

                var binding = TaskListBinding.Create(input.UserId, list.Id, input.AccountId, id, timeProvider);
                await bindings.AddAsync(binding, token);
                created.Add(binding.ToDto(list));
            }

            IReadOnlyList<TaskListBindingDto> result = created;
            return Result<IReadOnlyList<TaskListBindingDto>>.Success(result);
        }, cancellationToken: ct);
    }
}
