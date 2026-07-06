using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.AbortImportFile;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RetryImportFile;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadImportFile;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFile;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFiles;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportRows;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Finances.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/finances/imports")]
public sealed class ImportsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        [FromForm] Guid? accountId,
        [FromForm] Guid? cardId,
        [FromForm] DateOnly? cutoffDate,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var result = await sender.Send(new UploadImportFileCommand(new UploadImportFileInput(
            UserId, accountId, cardId, file.FileName, bytes, cutoffDate)), ct);

        return result.ToActionResult(errorMapper);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetImportFilesQuery(new GetImportFilesInput(UserId, new ImportFileFilter(skip, take))), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetImportFileQuery(new GetImportFileInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/rows")]
    public async Task<IActionResult> GetRowsAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetImportRowsQuery(new GetImportRowsInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/abort")]
    public async Task<IActionResult> AbortAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new AbortImportFileCommand(new AbortImportFileInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> RetryAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new RetryImportFileCommand(new RetryImportFileInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }
}
