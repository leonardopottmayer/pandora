using Microsoft.AspNetCore.Mvc.Filters;
using Pottmayer.Pandora.Modules.Admin.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Admin.Presentation.Filters;

/// <summary>
/// The panel can change what everyone may do, so only an account with two-step verification uses it.
/// Checked here rather than at sign-in, so Identity never needs to know about roles.
/// </summary>
public sealed class RequireMfaFilter(
    IUserContextAccessor<UserData> userContextAccessor,
    IUserDirectory directory,
    IHttpErrorMapper errorMapper) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var user = await directory.FindAsync(userId, context.HttpContext.RequestAborted);
        if (user is not { MfaEnabled: true })
        {
            context.Result = Result.Failure(AdminErrors.MfaRequired).ToActionResult(errorMapper);
            return;
        }

        await next();
    }
}
