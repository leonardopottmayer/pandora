using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetAttachment;

/// <remarks>Re-fetches the whole raw message each time: the cache keeps bodies, not parts (a known M1 limit).</remarks>
public sealed class GetAttachmentQueryHandler(IUnitOfWorkFactory factory, IServiceProvider services)
    : QueryHandlerBase<GetAttachmentQuery, AttachmentFile>
{
    protected override async Task<Result<AttachmentFile>> HandleAsync(GetAttachmentQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var found = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            MessageReads.FindAsync(ctx, input.UserId, input.MessageId, token), cancellationToken: ct);
        if (found is not { } pair)
            return Fail(MailErrors.MessageNotFound);

        var (message, mailbox) = pair;

        var raw = await services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value)
            .GetRawMessageAsync(mailbox, message.ProviderMessageId, ct);
        if (raw.IsFailure)
            return Result<AttachmentFile>.Failure(raw.Errors);

        return MimeReader.FindAttachment(raw.Value, input.AttachmentId) is { } part
            ? Ok(new AttachmentFile(part.Content, part.ContentType, part.FileName))
            : Fail(MailErrors.AttachmentNotFound);
    }
}
