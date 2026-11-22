using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.TransformText;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.AskNotes;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.GenerateText;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>AI writing previews; this controller never writes a page.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/ai")]
public sealed class AiController(ISender sender, IHttpErrorMapper errorMapper, IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateAsync(GenerateTextRequest request, CancellationToken ct)
        => (await sender.Send(new GenerateTextCommand(new GenerateTextInput(
            userContextAccessor.Context.User!.Id, request.Prompt)), ct)).ToActionResult(errorMapper);

    [HttpPost("ask")]
    public async Task<IActionResult> AskAsync(AskNotesRequest request, CancellationToken ct)
        => (await sender.Send(new AskNotesCommand(new AskNotesInput(
            userContextAccessor.Context.User!.Id, request.Question, request.SpaceId)), ct)).ToActionResult(errorMapper);

    [HttpPost("transform")]
    public async Task<IActionResult> TransformAsync(TransformTextRequest request, CancellationToken ct)
        => (await sender.Send(new TransformTextCommand(new TransformTextInput(
            userContextAccessor.Context.User!.Id, request.Action, request.Text, request.Language, request.Context)), ct)).ToActionResult(errorMapper);
}
