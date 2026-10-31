using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetComposeDraft;

/// <param name="Mode"><c>reply</c>, <c>reply-all</c> or <c>forward</c>.</param>
public sealed record GetComposeDraftInput(Guid UserId, Guid MessageId, string? Mode);

/// <summary>
/// The form for a reply or a forward, worked out from the raw message: the mirror has no <c>Reply-To</c>, and getting that
/// wrong sends mail to the wrong person.
/// </summary>
public sealed class GetComposeDraftQuery(GetComposeDraftInput input) : QueryBase<GetComposeDraftInput, ComposeDraft>(input);

public sealed class GetComposeDraftQueryHandler(IUnitOfWorkFactory factory, IServiceProvider services)
    : QueryHandlerBase<GetComposeDraftQuery, ComposeDraft>
{
    protected override async Task<Result<ComposeDraft>> HandleAsync(GetComposeDraftQuery request, CancellationToken ct)
    {
        var input = request.Input;
        ComposeMode? mode = input.Mode switch
        {
            "reply" => ComposeMode.Reply,
            "reply-all" => ComposeMode.ReplyAll,
            "forward" => ComposeMode.Forward,
            _ => null,
        };
        if (mode is null)
            return Fail(MailErrors.ActionNotSupported);

        var found = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            MessageReads.FindAsync(ctx, input.UserId, input.MessageId, token), cancellationToken: ct);
        if (found is not { } pair)
            return Fail(MailErrors.MessageNotFound);

        var raw = await services.GetRequiredKeyedService<IMailboxProvider>(pair.Mailbox.Provider.Value)
            .GetRawMessageAsync(pair.Mailbox, pair.Message.ProviderMessageId, ct);
        return raw.IsFailure
            ? Result<ComposeDraft>.Failure(raw.Errors)
            : Ok(MessageComposer.DraftFrom(raw.Value, pair.Mailbox.Address, mode.Value));
    }
}
