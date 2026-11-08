using System.Globalization;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.OpenDailyNote;

public sealed class OpenDailyNoteCommandHandler(IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : CommandHandlerBase<OpenDailyNoteCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(OpenDailyNoteCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var date = input.Date ?? await timeZones.ResolveTodayAsync(input.UserId, timeProvider.GetUtcNow(), ct);
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var preferencesRepo = ctx.AcquireRepository<INotesPreferencesRepository>();
            await using var userLock = await preferencesRepo.LockForUserAsync(input.UserId, token);
            var pages = ctx.AcquireRepository<IPageRepository>();
            var dailyNotes = ctx.AcquireRepository<IDailyNoteRepository>();
            var daily = await dailyNotes.FindForDayAsync(input.UserId, date, token);
            if (daily is not null && await pages.FindByIdForUserAsync(daily.PageId, input.UserId, token) is { } existing)
                return Result<PageDto>.Success(PageDto.From(existing, await PageTagReader.LoadAsync(ctx, existing.Id, input.UserId, token)));

            var preferences = await preferencesRepo.GetByIdAsync(input.UserId, token);
            var template = preferences?.DailyTemplateId is { } templateId
                ? await pages.FindByIdForUserAsync(templateId, input.UserId, token) : null;
            var title = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var content = template is { IsTemplate: true, IsArchived: false }
                ? PageContent.Parse(template.Content).InstantiateTemplate(title, date) : PageContent.Empty;
            if (!PageContent.TryParse(content.Json, out content, out var problem))
                return Result<PageDto>.Failure([PageErrors.InvalidContent(problem!)]);

            var space = await SpaceResolver.ResolveAsync(ctx.AcquireRepository<ISpaceRepository>(), input.UserId, null, timeProvider, token);
            if (space.IsFailure)
                return Result<PageDto>.Failure([.. space.Errors]);
            var folders = ctx.AcquireRepository<IFolderRepository>();
            var folder = preferences?.DailyFolderId is { } folderId
                ? await folders.FindByIdForUserAsync(folderId, input.UserId, token) : null;
            if (folder is null || folder.SpaceId != space.Value!.Id)
            {
                var name = input.Locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "Diário" : "Daily";
                folder = (await folders.GetForUserAsync(input.UserId, space.Value!.Id, token))
                    .FirstOrDefault(candidate => candidate.ParentId is null && candidate.Name == name);
                if (folder is null)
                {
                    folder = Folder.Create(input.UserId, space.Value!.Id, null, name, null, null, 0, timeProvider);
                    await folders.AddAsync(folder, token);
                }
            }
            if (preferences is null)
            {
                preferences = NotesPreferences.Create(input.UserId);
                await preferencesRepo.AddAsync(preferences, token);
            }
            preferences.SetDailyFolder(folder.Id);
            var slug = title;
            for (var suffix = 2; await pages.ExistsWithSlugAsync(input.UserId, slug, token); suffix++)
                slug = $"{title}-{suffix}";
            var page = Page.Create(input.UserId, space.Value!.Id, title, slug, null,
                template is { IsTemplate: true, IsArchived: false } ? template.Icon : null, 0, content, timeProvider, folder.Id);
            await pages.AddAsync(page, token);
            await PageLinkSynchronizer.RebuildAsync(page, content, pages, ctx.AcquireRepository<IPageLinkRepository>(), timeProvider, token);
            await PageMediaSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<IMediaItemRepository>(), ctx.AcquireRepository<IPageMediaRepository>(), timeProvider, token);
            await BlockStampSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<IBlockStampRepository>(), timeProvider, token);
            var tags = await PageTagSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<ITagRepository>(), ctx.AcquireRepository<IPageTagRepository>(), timeProvider, token);
            if (daily is null)
                await dailyNotes.AddAsync(DailyNote.Create(input.UserId, date, page.Id), token);
            else
                daily.ReplacePage(page.Id);
            // Commit before releasing the lock: a competing visit must see the complete page and registry.
            await ctx.CommitAsync(token);
            return Result<PageDto>.Success(PageDto.From(page, tags));
        }, new UnitOfWorkOptions { CommitOnSuccess = false }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
