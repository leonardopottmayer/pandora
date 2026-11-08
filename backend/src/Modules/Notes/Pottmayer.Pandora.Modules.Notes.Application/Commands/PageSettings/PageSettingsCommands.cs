using System.Security.Cryptography;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.PageSettings;

// What a page is besides its blocks: how it looks, whether it is locked, verified or public, and
// when it was last opened. None of it is part of the page's versions.

/// <summary>Runs <paramref name="change"/> on one of the user's pages and answers the page.</summary>
internal static class PageSettingsChange
{
    public static async Task<Result<PageDto>> RunAsync(
        IUnitOfWorkFactory factory, Guid userId, Guid pageId, Func<Page, IDataContext, CancellationToken, Task> change, CancellationToken ct)
    {
        return await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();
            var page = await repo.FindByIdForUserAsync(pageId, userId, token);
            if (page is null)
                return Result<PageDto>.Failure([PageErrors.NotFound]);

            await change(page, ctx, token);
            await repo.UpdateAsync(page, token);
            return Result<PageDto>.Success(PageDto.From(page, await PageTagReader.LoadAsync(ctx, page.Id, userId, token)));
        }, cancellationToken: ct);
    }
}

// ---------- Appearance ----------

public sealed record SetPageAppearanceInput(Guid UserId, Guid PageId, PageCover? Cover, string? Width, string? Font, bool SmallText);

/// <summary>Sets a page's cover, width, font and text size. A library cover counts as a use of the item.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class SetPageAppearanceCommand(SetPageAppearanceInput input) : CommandBase<SetPageAppearanceInput, PageDto>(input);

public sealed class SetPageAppearanceCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetPageAppearanceCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(SetPageAppearanceCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!PageAppearance.TryCreate(input.Cover, input.Width, input.Font, input.SmallText, out var appearance, out var problem))
            return Fail(PageErrors.InvalidAppearance(problem!));

        var result = await PageSettingsChange.RunAsync(factory, input.UserId, input.PageId, async (page, ctx, token) =>
        {
            page.SetAppearance(appearance);
            await PageMediaSynchronizer.RebuildAsync(
                page, PageContent.Parse(page.Content), ctx.AcquireRepository<IMediaItemRepository>(), ctx.AcquireRepository<IPageMediaRepository>(),
                timeProvider, token);
        }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

// ---------- Lock ----------

public sealed record SetPageLockedInput(Guid UserId, Guid PageId, bool Locked);

/// <summary>Locks a page to read it without editing by accident, or unlocks it.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class SetPageLockedCommand(SetPageLockedInput input) : CommandBase<SetPageLockedInput, PageDto>(input);

public sealed class SetPageLockedCommandHandler(IUnitOfWorkFactory factory) : CommandHandlerBase<SetPageLockedCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(SetPageLockedCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await PageSettingsChange.RunAsync(factory, input.UserId, input.PageId, (page, _, _) =>
        {
            page.SetLocked(input.Locked);
            return Task.CompletedTask;
        }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

// ---------- Verification ----------

/// <param name="Verified">False clears it.</param>
/// <param name="Days">How long it holds, 1 to 365; null when it does not run out.</param>
public sealed record SetPageVerificationInput(Guid UserId, Guid PageId, bool Verified, int? Days);

/// <summary>Marks a page as checked now, for some days or for good, or clears the mark.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class SetPageVerificationCommand(SetPageVerificationInput input) : CommandBase<SetPageVerificationInput, PageDto>(input);

public sealed class SetPageVerificationCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetPageVerificationCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(SetPageVerificationCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.Verified && input.Days is < 1 or > 365)
            return Fail(PageErrors.InvalidVerification);

        var result = await PageSettingsChange.RunAsync(factory, input.UserId, input.PageId, (page, _, _) =>
        {
            if (input.Verified)
                page.Verify(input.Days is { } days ? timeProvider.GetUtcNow().AddDays(days) : null, timeProvider);
            else
                page.ClearVerification();
            return Task.CompletedTask;
        }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

// ---------- Public link ----------

public sealed record SetPagePublicInput(Guid UserId, Guid PageId, bool Public);

/// <summary>
/// Opens a read-only public link to the page, or closes it. Opening an open link keeps its token;
/// closing and opening again makes a new one, so an old link stops working.
/// </summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class SetPagePublicCommand(SetPagePublicInput input) : CommandBase<SetPagePublicInput, PageDto>(input);

public sealed class SetPagePublicCommandHandler(IUnitOfWorkFactory factory) : CommandHandlerBase<SetPagePublicCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(SetPagePublicCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await PageSettingsChange.RunAsync(factory, input.UserId, input.PageId, (page, _, _) =>
        {
            if (!input.Public)
                page.SetPublicToken(null);
            else if (page.PublicToken is null)
                // 32 random bytes: a link no one finds by guessing.
                page.SetPublicToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
            return Task.CompletedTask;
        }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

// ---------- Viewed ----------

public sealed record MarkPageViewedInput(Guid UserId, Guid PageId);

/// <summary>Notes that the page was opened, for the recent pages.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class MarkPageViewedCommand(MarkPageViewedInput input) : CommandBase<MarkPageViewedInput, bool>(input);

public sealed class MarkPageViewedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<MarkPageViewedCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(MarkPageViewedCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var found = await factory.ExecuteAsync(NotesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IPageRepository>().MarkViewedAsync(input.PageId, input.UserId, timeProvider.GetUtcNow(), token), cancellationToken: ct);
        return found ? Ok(true) : Fail(PageErrors.NotFound);
    }
}
