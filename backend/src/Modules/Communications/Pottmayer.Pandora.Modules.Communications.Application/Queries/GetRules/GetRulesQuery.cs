using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetRules;

public sealed record GetRulesInput(Guid UserId, Guid MailboxId);

/// <summary>In position order.</summary>
public sealed class GetRulesQuery(GetRulesInput input)
    : QueryBase<GetRulesInput, IReadOnlyList<RuleDto>>(input);

public sealed class GetRulesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetRulesQuery, IReadOnlyList<RuleDto>>
{
    protected override async Task<Result<IReadOnlyList<RuleDto>>> HandleAsync(GetRulesQuery request, CancellationToken ct)
    {
        var input = request.Input;
        return await factory.ExecuteAsync<Result<IReadOnlyList<RuleDto>>>(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var mailbox = await ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(input.MailboxId, input.UserId, token);
            if (mailbox is null)
                return MailErrors.MailboxNotFound;

            var rules = await ctx.AcquireRepository<IMailRuleRepository>().ListByMailboxAsync(mailbox.Id, token);
            return Result<IReadOnlyList<RuleDto>>.Success(rules.Select(RuleDto.From).ToList());
        }, cancellationToken: ct);
    }
}
