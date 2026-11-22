using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SaveNotesPreferences;

public sealed class SaveNotesPreferencesCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<SaveNotesPreferencesCommand, NotesPreferencesDto>
{
    protected override async Task<Result<NotesPreferencesDto>> HandleAsync(SaveNotesPreferencesCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<INotesPreferencesRepository>();
            await using var userLock = await repo.LockForUserAsync(input.UserId, token);
            if (input.DailyTemplateId is { } id)
            {
                var model = await ctx.AcquireRepository<IPageRepository>().FindByIdForUserAsync(id, input.UserId, token);
                if (model is null || !model.IsTemplate || model.IsArchived)
                    return Result<NotesPreferencesDto>.Failure([PageErrors.NotFound]);
            }
            var preferences = await repo.GetByIdAsync(input.UserId, token);
            if (preferences is null)
            {
                preferences = NotesPreferences.Create(input.UserId);
                await repo.AddAsync(preferences, token);
            }
            preferences.SetDailyTemplate(input.DailyTemplateId);
            await ctx.CommitAsync(token);
            return Result<NotesPreferencesDto>.Success(new(preferences.DailyTemplateId));
        }, new UnitOfWorkOptions { CommitOnSuccess = false }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
