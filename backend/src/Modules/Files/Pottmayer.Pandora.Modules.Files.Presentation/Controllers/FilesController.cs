using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Commands.AddRoot;
using Pottmayer.Pandora.Modules.Files.Application.Commands.DecideReview;
using Pottmayer.Pandora.Modules.Files.Application.Commands.DeleteFilter;
using Pottmayer.Pandora.Modules.Files.Application.Commands.RemoveRoot;
using Pottmayer.Pandora.Modules.Files.Application.Commands.ResolveHeldScan;
using Pottmayer.Pandora.Modules.Files.Application.Commands.SaveFilter;
using Pottmayer.Pandora.Modules.Files.Application.Commands.SavePreferences;
using Pottmayer.Pandora.Modules.Files.Application.Commands.SaveSelection;
using Pottmayer.Pandora.Modules.Files.Application.Commands.UpdateRoot;
using Pottmayer.Pandora.Modules.Files.Application.Queries.BrowseEntries;
using Pottmayer.Pandora.Modules.Files.Application.Queries.GetEntry;
using Pottmayer.Pandora.Modules.Files.Application.Queries.GetFilters;
using Pottmayer.Pandora.Modules.Files.Application.Queries.GetPreferences;
using Pottmayer.Pandora.Modules.Files.Application.Queries.GetReviewTree;
using Pottmayer.Pandora.Modules.Files.Application.Queries.GetRoots;
using Pottmayer.Pandora.Modules.Files.Application.Queries.GetScans;
using Pottmayer.Pandora.Modules.Files.Application.Queries.PreviewFilter;
using Pottmayer.Pandora.Modules.Files.Application.Queries.SearchEntries;
using Pottmayer.Pandora.Modules.Files.Domain.ReadModels;
using Pottmayer.Pandora.Modules.Files.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Files.Presentation.Controllers;

/// <summary>The user's side of Files: the account switch, roots, selection, filters, browsing, search and review.</summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/files")]
public sealed class FilesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    // ── Preferences ──

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferencesAsync(CancellationToken ct) =>
        (await sender.Send(new GetPreferencesQuery(new GetPreferencesInput(UserId)), ct)).ToActionResult(errorMapper);

    [HttpPut("preferences")]
    public async Task<IActionResult> SavePreferencesAsync(SavePreferencesRequest body, CancellationToken ct) =>
        (await sender.Send(new SavePreferencesCommand(new SavePreferencesInput(UserId, body.IsEnabled)), ct)).ToActionResult(errorMapper);

    // ── Roots ──

    [HttpGet("roots")]
    public async Task<IActionResult> GetRootsAsync(CancellationToken ct) =>
        (await sender.Send(new GetRootsQuery(new GetRootsInput(UserId)), ct)).ToActionResult(errorMapper);

    [HttpPost("roots")]
    public async Task<IActionResult> AddRootAsync(AddRootRequest body, CancellationToken ct) =>
        (await sender.Send(new AddRootCommand(new AddRootInput(
            UserId, body.DeviceId, body.Name, body.LocalPath, body.ScanTime, body.IncludeHidden, body.CaseSensitive)), ct))
        .ToActionResult(errorMapper);

    [HttpPatch("roots/{id:guid}")]
    public async Task<IActionResult> UpdateRootAsync(Guid id, UpdateRootRequest body, CancellationToken ct) =>
        (await sender.Send(new UpdateRootCommand(new UpdateRootInput(
            UserId, id, body.Name, body.ScanTime, body.IncludeHidden, body.CaseSensitive)), ct))
        .ToActionResult(errorMapper);

    /// <summary>Stops scanning the root; its entries go to the review inbox as excluded.</summary>
    [HttpDelete("roots/{id:guid}")]
    public async Task<IActionResult> RemoveRootAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new RemoveRootCommand(new RemoveRootInput(UserId, id)), ct)).ToActionResult(errorMapper);

    /// <summary>Replaces the selection marks as a set.</summary>
    [HttpPut("roots/{id:guid}/selection")]
    public async Task<IActionResult> SaveSelectionAsync(Guid id, SaveSelectionRequest body, CancellationToken ct) =>
        (await sender.Send(new SaveSelectionCommand(new SaveSelectionInput(UserId, id, body.Marks)), ct)).ToActionResult(errorMapper);

    [HttpGet("roots/{id:guid}/entries")]
    public async Task<IActionResult> BrowseAsync(
        Guid id, [FromQuery] string? parentPath, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default) =>
        (await sender.Send(new BrowseEntriesQuery(new BrowseEntriesInput(UserId, id, parentPath, skip, take)), ct))
        .ToActionResult(errorMapper);

    // ── Filters ──

    [HttpGet("filters")]
    public async Task<IActionResult> GetFiltersAsync(CancellationToken ct) =>
        (await sender.Send(new GetFiltersQuery(new GetFiltersInput(UserId)), ct)).ToActionResult(errorMapper);

    [HttpPost("filters")]
    public async Task<IActionResult> AddFilterAsync(FilterRequest body, CancellationToken ct) =>
        (await sender.Send(new SaveFilterCommand(new SaveFilterInput(UserId, null, ToInput(body))), ct)).ToActionResult(errorMapper);

    [HttpPatch("filters/{id:guid}")]
    public async Task<IActionResult> UpdateFilterAsync(Guid id, FilterRequest body, CancellationToken ct) =>
        (await sender.Send(new SaveFilterCommand(new SaveFilterInput(UserId, id, ToInput(body))), ct)).ToActionResult(errorMapper);

    [HttpDelete("filters/{id:guid}")]
    public async Task<IActionResult> DeleteFilterAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new DeleteFilterCommand(new DeleteFilterInput(UserId, id)), ct)).ToActionResult(errorMapper);

    /// <summary>What a draft filter would match in the catalog now: a count and a sample.</summary>
    [HttpPost("filters/preview")]
    public async Task<IActionResult> PreviewFilterAsync(FilterRequest body, CancellationToken ct) =>
        (await sender.Send(new PreviewFilterQuery(new PreviewFilterInput(UserId, ToInput(body))), ct)).ToActionResult(errorMapper);

    // ── Catalog ──

    [HttpGet("search")]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string? q, [FromQuery] Guid? deviceId, [FromQuery] Guid? rootId, [FromQuery] string? category,
        [FromQuery] long? minSize, [FromQuery] long? maxSize, [FromQuery] DateTimeOffset? modifiedFrom,
        [FromQuery] DateTimeOffset? modifiedTo, [FromQuery] string? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default) =>
        (await sender.Send(new SearchEntriesQuery(new SearchEntriesInput(
            UserId, q, deviceId, rootId, category, minSize, maxSize, modifiedFrom, modifiedTo, status, skip, take)), ct))
        .ToActionResult(errorMapper);

    [HttpGet("entries/{id:guid}")]
    public async Task<IActionResult> GetEntryAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new GetEntryQuery(new GetEntryInput(UserId, id)), ct)).ToActionResult(errorMapper);

    // ── Scans ──

    [HttpGet("scans")]
    public async Task<IActionResult> GetScansAsync([FromQuery] Guid rootId, CancellationToken ct) =>
        (await sender.Send(new GetScansQuery(new GetScansInput(UserId, rootId)), ct)).ToActionResult(errorMapper);

    /// <summary>Applies a scan the safety brake held.</summary>
    [HttpPost("scans/{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmScanAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new ResolveHeldScanCommand(new ResolveHeldScanInput(UserId, id, true)), ct)).ToActionResult(errorMapper);

    /// <summary>Drops a scan the safety brake held; nothing is marked missing.</summary>
    [HttpPost("scans/{id:guid}/discard")]
    public async Task<IActionResult> DiscardScanAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new ResolveHeldScanCommand(new ResolveHeldScanInput(UserId, id, false)), ct)).ToActionResult(errorMapper);

    // ── Review inbox ──

    [HttpGet("review/tree")]
    public async Task<IActionResult> GetReviewTreeAsync([FromQuery] Guid? rootId, [FromQuery] string? parentPath, CancellationToken ct) =>
        (await sender.Send(new GetReviewTreeQuery(new GetReviewTreeInput(UserId, rootId, parentPath)), ct)).ToActionResult(errorMapper);

    /// <summary>Removes the entries from the catalog — the only way an entry ever leaves it.</summary>
    [HttpPost("review/forget")]
    public Task<IActionResult> ForgetAsync(ReviewDecisionRequest body, CancellationToken ct) =>
        DecideAsync(ReviewDecision.Forget, body, ct);

    /// <summary>Keeps the entries in the catalog as missing or excluded, out of the inbox.</summary>
    [HttpPost("review/keep")]
    public Task<IActionResult> KeepAsync(ReviewDecisionRequest body, CancellationToken ct) =>
        DecideAsync(ReviewDecision.Keep, body, ct);

    private async Task<IActionResult> DecideAsync(ReviewDecision decision, ReviewDecisionRequest body, CancellationToken ct) =>
        (await sender.Send(new DecideReviewCommand(new DecideReviewInput(UserId, decision, body.EntryIds, body.Folders)), ct))
        .ToActionResult(errorMapper);

    private static FilterInput ToInput(FilterRequest r) => new(
        r.DeviceId, r.RootId, r.ScopePath, r.Name, r.Action, r.AppliesTo, r.Matcher, r.Pattern, r.CaseSensitive, r.IsEnabled);
}
