using System.Globalization;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions.Capture;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CaptureNote;

/// <summary>Commits the page, derived data, history and event receipt together.</summary>
public sealed class CaptureNoteCommandHandler(IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : CommandHandlerBase<CaptureNoteCommand, NoteCaptureResult>
{
    protected override async Task<Result<NoteCaptureResult>> HandleAsync(CaptureNoteCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.MessageId == Guid.Empty || string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 8000)
            return Fail(CaptureErrors.InvalidText);
        var title = input.Locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "Captura" : "Capture";
        var zone = await timeZones.ResolveAsync(input.UserId, ct: ct);
        var date = TimeZoneInfo.ConvertTime(input.ReceivedAt, zone).ToString("dd/MM/yyyy HH:mm zzz", CultureInfo.InvariantCulture);
        var addition = PageContent.Parse(JsonSerializer.Serialize(new { version = 1, blocks = new[] {
            new { id = input.MessageId.ToString("N"), type = "paragraph", content = new[] { new { text = $"[{date}] {input.Text.Trim()}" } } }
        } }));

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var captures = ctx.AcquireRepository<INoteCaptureRepository>();
            var pages = ctx.AcquireRepository<IPageRepository>();
            var receipt = await captures.GetByIdAsync(input.MessageId, token);
            if (receipt is not null)
            {
                if (receipt.UserId != input.UserId)
                    return Result<NoteCaptureResult>.Failure(PageErrors.NotFound);
                var destination = await pages.FindByIdForUserAsync(receipt.PageId, input.UserId, token);
                return Result<NoteCaptureResult>.Success(new(receipt.PageId, destination?.Title ?? title, IsDuplicate: true));
            }

            var page = await captures.FindInboxAsync(input.UserId, token);
            if (page?.IsLocked == true)
                return Result<NoteCaptureResult>.Failure(CaptureErrors.Locked);
            if (page is null)
            {
                var space = await SpaceResolver.ResolveAsync(ctx.AcquireRepository<ISpaceRepository>(), input.UserId, null, timeProvider, token);
                if (space.IsFailure)
                    return Result<NoteCaptureResult>.Failure([.. space.Errors]);
                var baseSlug = Slugger.From(title);
                var slug = baseSlug;
                for (var suffix = 2; await pages.ExistsWithSlugAsync(input.UserId, slug, token); suffix++)
                    slug = $"{baseSlug}-{suffix}";
                page = Page.Create(input.UserId, space.Value!.Id, title, slug, null, "📥", 0, addition, timeProvider);
                await pages.AddAsync(page, token);
            }
            else
            {
                var combined = PageContent.Parse(page.Content).Append(addition);
                if (!PageContent.TryParse(combined.Json, out var valid, out var problem))
                    return Result<NoteCaptureResult>.Failure(PageErrors.InvalidContent(problem!));
                page.Update(page.Title, page.Icon, valid);
                await pages.UpdateAsync(page, token);
            }
            page.SetHistorySource(PageVersionSource.Assistant);
            var content = PageContent.Parse(page.Content);
            await PageLinkSynchronizer.RebuildAsync(page, content, pages, ctx.AcquireRepository<IPageLinkRepository>(), timeProvider, token);
            await PageMediaSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<IMediaItemRepository>(), ctx.AcquireRepository<IPageMediaRepository>(), timeProvider, token);
            await PageTagSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<ITagRepository>(), ctx.AcquireRepository<IPageTagRepository>(), timeProvider, token);
            await BlockStampSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<IBlockStampRepository>(), timeProvider, token);
            await captures.AddAsync(NoteCapture.Record(input.MessageId, input.UserId, page.Id, timeProvider), token);
            return Result<NoteCaptureResult>.Success(new(page.Id, page.Title, IsDuplicate: false));
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
