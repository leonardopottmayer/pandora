using Pottmayer.Tars.Core.Localization.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Web.Http;
using Pottmayer.Tars.Web.Http.Abstractions;

namespace Pottmayer.Pandora.Host.Localization;

public sealed class LocalizedHttpErrorMapper(IMessageProvider messages) : IHttpErrorMapper
{
    private const string InternalServerErrorKey = "tars.http.internal_server_error";

    public int MapToStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.NotFound     => 404,
        ErrorType.Validation   => 422,
        ErrorType.Business     => 400,
        ErrorType.Conflict     => 409,
        ErrorType.Unauthorized => 401,
        ErrorType.Forbidden    => 403,
        _                      => 500
    };

    public IHttpErrorResponse Map(Error error) => new HttpErrorResponse
    {
        Success      = false,
        ErrorCode    = error.Code,
        ErrorMessage = messages.Get(error.Code, fallback: error.Message),
        FieldErrors  = BuildFieldErrors(error)
    };

    public IHttpErrorResponse Map(Exception exception) => new HttpErrorResponse
    {
        Success      = false,
        ErrorCode    = "INTERNAL_SERVER_ERROR",
        ErrorMessage = messages.Get(InternalServerErrorKey)
    };

    private static IReadOnlyList<IHttpFieldError>? BuildFieldErrors(Error error)
    {
        if (error.Type != ErrorType.Validation || error.Metadata is null)
            return null;

        var list = error.Metadata
            .Select(kv => (IHttpFieldError)new HttpFieldError(kv.Key, kv.Value?.ToString() ?? string.Empty))
            .ToList();

        return list.Count > 0 ? list : null;
    }
}
