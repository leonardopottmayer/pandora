using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetLinkPreview;

public sealed class GetLinkPreviewQueryHandler(ILinkPreviewer previewer)
    : QueryHandlerBase<GetLinkPreviewQuery, LinkPreview>
{
    private const int MaxUrlLength = 2048;

    protected override async Task<Result<LinkPreview>> HandleAsync(GetLinkPreviewQuery request, CancellationToken ct)
    {
        var raw = request.Input.Url?.Trim() ?? string.Empty;
        if (raw.Length > MaxUrlLength
            || !Uri.TryCreate(raw, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            return Fail(LinkErrors.InvalidUrl);

        var preview = await previewer.PreviewAsync(url, ct);
        return preview is null ? Fail(LinkErrors.Unreachable) : Ok(preview);
    }
}
