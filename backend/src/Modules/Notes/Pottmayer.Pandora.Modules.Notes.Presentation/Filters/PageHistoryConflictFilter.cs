using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Filters;

public sealed class PageHistoryConflictFilter(IHttpErrorMapper errorMapper) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not PageHistoryConflictException)
            return;

        context.Result = Result<object>.Failure([PageErrors.HistoryConflict]).ToActionResult(errorMapper);
        context.ExceptionHandled = true;
    }
}
