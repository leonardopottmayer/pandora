using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Commands.AbortScan;
using Pottmayer.Pandora.Modules.Files.Application.Commands.CompleteScan;
using Pottmayer.Pandora.Modules.Files.Application.Commands.RecordBatch;
using Pottmayer.Pandora.Modules.Files.Application.Commands.StartScan;
using Pottmayer.Pandora.Modules.Files.Application.Queries.GetAgentConfig;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Files.Presentation.Controllers;

/// <summary>
/// The agent protocol (product-plan §4.4). Reachable only with a device key, and only for the key's own
/// device: any paired device may call it, and the user decides what it does by giving it roots.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = DeviceAuthorization.Scheme, Policy = DeviceAuthorization.Policy)]
[Route("api/v{version:apiVersion}/files/agent")]
public sealed class FilesAgentController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;
    private Guid DeviceId => Guid.Parse(User.FindFirst(DeviceAuthorization.DeviceIdClaim)!.Value);

    [HttpGet("config")]
    public async Task<IActionResult> GetConfigAsync(CancellationToken ct) =>
        (await sender.Send(new GetAgentConfigQuery(new GetAgentConfigInput(UserId, DeviceId)), ct)).ToActionResult(errorMapper);

    [HttpPost("scans")]
    public async Task<IActionResult> StartScanAsync(StartScanRequest body, CancellationToken ct) =>
        (await sender.Send(new StartScanCommand(new StartScanInput(UserId, DeviceId, body.RootId)), ct)).ToActionResult(errorMapper);

    [HttpPost("scans/{id:guid}/batches")]
    public async Task<IActionResult> RecordBatchAsync(Guid id, ScanBatch body, CancellationToken ct) =>
        (await sender.Send(new RecordBatchCommand(new RecordBatchInput(UserId, DeviceId, id, body.Entries)), ct)).ToActionResult(errorMapper);

    [HttpPost("scans/{id:guid}/complete")]
    public async Task<IActionResult> CompleteScanAsync(Guid id, CompleteScanRequest body, CancellationToken ct) =>
        (await sender.Send(new CompleteScanCommand(new CompleteScanInput(UserId, DeviceId, id, body.EntriesSeen)), ct)).ToActionResult(errorMapper);

    [HttpPost("scans/{id:guid}/abort")]
    public async Task<IActionResult> AbortScanAsync(Guid id, AbortScanRequest body, CancellationToken ct) =>
        (await sender.Send(new AbortScanCommand(new AbortScanInput(UserId, DeviceId, id, body.Reason)), ct)).ToActionResult(errorMapper);
}
