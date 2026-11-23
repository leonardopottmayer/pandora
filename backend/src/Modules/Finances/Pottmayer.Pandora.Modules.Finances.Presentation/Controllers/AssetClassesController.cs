using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAssetClass;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAssetClassArchived;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAssetClass;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAssetClasses;
using Pottmayer.Pandora.Modules.Finances.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Finances.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/finances/asset-classes")]
public sealed class AssetClassesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
        => (await sender.Send(new GetAssetClassesQuery(new GetAssetClassesInput(UserId)), ct)).ToActionResult(errorMapper);

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateAssetClassRequest request, CancellationToken ct)
        => (await sender.Send(new CreateAssetClassCommand(new CreateAssetClassInput(UserId, request.Name, request.Behavior)), ct))
            .ToActionResult(errorMapper);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> RenameAsync(Guid id, RenameAssetClassRequest request, CancellationToken ct)
        => (await sender.Send(new UpdateAssetClassCommand(new UpdateAssetClassInput(UserId, id, request.Name)), ct))
            .ToActionResult(errorMapper);

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new SetAssetClassArchivedCommand(new SetAssetClassArchivedInput(UserId, id, true)), ct))
            .ToActionResult(errorMapper);

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchiveAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new SetAssetClassArchivedCommand(new SetAssetClassArchivedInput(UserId, id, false)), ct))
            .ToActionResult(errorMapper);
}
