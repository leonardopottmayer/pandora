using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTag;

public sealed class CreateTagCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateTagCommand, TagDto>
{
    protected override async Task<Result<TagDto>> HandleAsync(CreateTagCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(TagErrors.InvalidName);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITagRepository>();

            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, null, token))
                return Result<Tag>.Failure([TagErrors.NameAlreadyExists]);

            var tag = Tag.Create(input.UserId, input.Name, input.Color, timeProvider);
            await repo.AddAsync(tag, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, TagEvents.EntityType, tag.Id, TagEvents.Created, now,
                new { name = tag.Name, color = tag.Color }, ct: token);

            return Result<Tag>.Success(tag);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TagDto.From(result.Value!));
    }
}
