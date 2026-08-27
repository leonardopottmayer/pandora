using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetTagColor;

public sealed partial class SetTagColorCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<SetTagColorCommand, TagDto>
{
    protected override async Task<Result<TagDto>> HandleAsync(SetTagColorCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // The color ends up inline in the frontend's styles, so only a hex literal is accepted.
        if (!string.IsNullOrWhiteSpace(input.Color) && !HexColorRegex().IsMatch(input.Color.Trim()))
            return Fail(TagErrors.InvalidColor);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITagRepository>();

            var tag = await repo.FindByIdForUserAsync(input.TagId, input.UserId, token);
            if (tag is null)
                return Result<TagDto>.Failure([TagErrors.NotFound]);

            tag.SetColor(input.Color);
            await repo.UpdateAsync(tag, token);

            var pageCount = (await ctx.AcquireRepository<IPageTagRepository>()
                .GetByTagsAsync([tag.Id], token)).Count;

            return Result<TagDto>.Success(TagDto.From(tag, pageCount));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }

    [GeneratedRegex(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();
}
