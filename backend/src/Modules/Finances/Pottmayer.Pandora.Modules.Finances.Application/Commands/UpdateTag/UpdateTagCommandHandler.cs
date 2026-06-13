using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateTag;

public sealed class UpdateTagCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateTagCommand, TagDto>
{
    protected override async Task<Result<TagDto>> HandleAsync(UpdateTagCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(TagErrors.InvalidName);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITagRepository>();

            var tag = await repo.FindByIdForUserAsync(input.TagId, input.UserId, token);
            if (tag is null)
                return Result<Tag>.Failure([TagErrors.NotFound]);

            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, tag.Id, token))
                return Result<Tag>.Failure([TagErrors.NameAlreadyExists]);

            var diff = new
            {
                name = new { old = tag.Name, @new = input.Name.Trim() },
                color = new { old = tag.Color, @new = input.Color }
            };

            tag.Update(input.Name, input.Color);
            await repo.UpdateAsync(tag, token);

            await ctx.RecordAsync(input.UserId, input.UserId, "tag", tag.Id, "tag.updated", now, diff, ct: token);

            return Result<Tag>.Success(tag);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TagDto.From(result.Value!));
    }
}
