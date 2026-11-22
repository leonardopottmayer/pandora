using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;

public sealed class OpenMessageCommandHandler(IUnitOfWorkFactory factory, IServiceProvider services, TimeProvider timeProvider)
    : CommandHandlerBase<OpenMessageCommand, MessageDto>
{
    protected override async Task<Result<MessageDto>> HandleAsync(OpenMessageCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // Cached: touch it and answer from it.
        var cached = await factory.ExecuteAsync<(MailMessage, Mailbox, MailContent?)?>(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            if (await MessageReads.FindAsync(ctx, input.UserId, input.MessageId, token) is not { } found)
                return null;

            var (message, mailbox) = found;
            var contents = ctx.AcquireRepository<IMailContentRepository>();
            var content = await contents.GetByIdAsync(message.Id, token);
            if (content is not null)
            {
                content.Touch(timeProvider.GetUtcNow());
                await contents.UpdateAsync(content, token);
            }
            return (message, mailbox, content);
        }, cancellationToken: ct);

        if (cached is not { } opened)
            return Fail(MailErrors.MessageNotFound);

        var (msg, box, existing) = opened;
        if (existing is not null)
            return Ok(ToDto(msg, existing));

        var raw = await services.GetRequiredKeyedService<IMailboxProvider>(box.Provider.Value)
            .GetRawMessageAsync(box, msg.ProviderMessageId, ct);
        if (raw.IsFailure)
            return Result<MessageDto>.Failure(raw.Errors);

        var body = MimeReader.Read(raw.Value);
        var created = MailContent.Create(msg.Id, body.TextBody, body.HtmlBody, body.Attachments, timeProvider.GetUtcNow());

        // ponytail: check-then-add, so two first opens at once both fetch and the second add fails;
        // an upsert if that ever shows up.
        await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var contents = ctx.AcquireRepository<IMailContentRepository>();
            if (await contents.GetByIdAsync(msg.Id, token) is null)
                await contents.AddAsync(created, token);

            var messages = ctx.AcquireRepository<IMailMessageRepository>();
            if ((await messages.ListByIdsAsync([msg.Id], token)).FirstOrDefault() is { } tracked && tracked.Snippet.Length == 0)
            {
                tracked.FillSnippet(body.Snippet);
                await messages.UpdateAsync(tracked, token);
            }
        }, cancellationToken: ct);

        msg.FillSnippet(body.Snippet);
        return Ok(ToDto(msg, created));
    }

    private static MessageDto ToDto(MailMessage message, MailContent content) => new(
        MessageSummaryDto.FromMessage(message), message.Rfc822MessageId, content.TextBody, content.HtmlBody, content.Attachments);
}
