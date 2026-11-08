using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetFolderProperties;

public sealed class GetFolderPropertiesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetFolderPropertiesQuery, FolderPropertiesDto>
{
    protected override async Task<Result<FolderPropertiesDto>> HandleAsync(GetFolderPropertiesQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            if (await ctx.AcquireRepository<IFolderRepository>().FindByIdForUserAsync(input.FolderId, input.UserId, token) is null)
                return Result<FolderPropertiesDto>.Failure([FolderErrors.NotFound]);
            var tagged = await TagFilter.MatchingPageIdsAsync(input.TagIds, ctx.AcquireRepository<IPageTagRepository>(), token);
            var pages = await ctx.AcquireRepository<IPageRepository>().GetInFolderAsync(
                input.UserId, input.FolderId, input.IncludeArchived, tagged, token);
            var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<FolderPropertyPageDto>();
            foreach (var page in pages)
            {
                var values = new Dictionary<string, string>();
                foreach (var property in PageContent.Parse(page.Content).Properties())
                {
                    if (!columns.TryGetValue(property.Key, out var key))
                    {
                        key = property.Key;
                        columns.Add(key, key);
                    }
                    // Repeated keys share a column but none of their values are silently discarded.
                    values[key] = values.TryGetValue(key, out var previous) ? previous + "\n" + property.Value : property.Value;
                }
                rows.Add(new FolderPropertyPageDto(PageSummaryDto.From(page), values));
            }
            return Result<FolderPropertiesDto>.Success(new([.. columns.Values], rows));
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
