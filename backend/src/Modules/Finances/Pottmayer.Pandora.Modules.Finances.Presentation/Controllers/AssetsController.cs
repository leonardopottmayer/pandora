using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAsset;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAssetArchived;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAsset;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAsset;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAssets;
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
[Route("api/v{version:apiVersion}/finances/assets")]
public sealed class AssetsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] Guid? classId = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => (await sender.Send(new GetAssetsQuery(new GetAssetsInput(UserId, classId, search)), ct)).ToActionResult(errorMapper);

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateAssetRequest request, CancellationToken ct)
        => (await sender.Send(new CreateAssetCommand(new CreateAssetInput(
            UserId, request.AssetClassId, request.Name, request.Code, request.Isin, request.Issuer, request.Currency)), ct))
            .ToActionResult(errorMapper);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateAssetRequest request, CancellationToken ct)
        => (await sender.Send(new UpdateAssetCommand(new UpdateAssetInput(UserId, id, request.Name, request.Code, request.Isin, request.Issuer, request.AssetClassId)), ct))
            .ToActionResult(errorMapper);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new DeleteAssetCommand(new DeleteAssetInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new SetAssetArchivedCommand(new SetAssetArchivedInput(UserId, id, true)), ct)).ToActionResult(errorMapper);

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchiveAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new SetAssetArchivedCommand(new SetAssetArchivedInput(UserId, id, false)), ct)).ToActionResult(errorMapper);
}
