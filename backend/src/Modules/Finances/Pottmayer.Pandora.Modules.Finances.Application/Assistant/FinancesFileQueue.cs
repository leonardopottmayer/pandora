using Pottmayer.Pandora.Modules.Assistant.Abstractions.Files;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadAttachment;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// The Finances queue for files shared with the assistant bot: a receipt or a boleto captioned
/// "comprovante luz setembro" lands as an unassigned attachment, its caption kept as the note, and the
/// user files it under a transaction, a suggestion or a statement in the app. The caption's words also
/// pick the kind (comprovante → receipt, boleto → bill, nota fiscal → invoice).
/// </summary>
public sealed class FinancesFileQueue(ISender sender) : IAssistantFileQueue
{
    private const int MaxNoteLength = 500;

    private static readonly string[] ReceiptWords = ["comprovante", "recibo", "pix", "pagamento", "receipt"];
    private static readonly string[] BillWords = ["boleto", "conta", "fatura", "bill"];
    private static readonly string[] InvoiceWords = ["nf", "nfe", "fiscal", "cupom", "invoice"];

    public string Name => "Finances";

    public IReadOnlyList<string> Keywords { get; } =
    [
        // The first three are the examples the bot offers when a caption names no queue.
        "comprovante", "boleto", "financeiro",
        "financas", "financa", "finances", "finance",
        .. ReceiptWords[1..], .. BillWords[1..], .. InvoiceWords,
    ];

    public async Task<bool> EnqueueAsync(SharedFile file, CancellationToken ct = default)
    {
        var note = file.Caption.Length > MaxNoteLength ? file.Caption[..MaxNoteLength] : file.Caption;
        var result = await sender.Send(new UploadAttachmentCommand(new UploadAttachmentInput(
            file.UserId, null, null, null, KindOf(file.Words).Value, file.FileName, file.ContentType, file.Content, note)), ct);
        return result.IsSuccess;
    }

    private static AttachmentKind KindOf(IReadOnlySet<string> words) =>
        ReceiptWords.Any(words.Contains) ? AttachmentKind.Receipt
        : BillWords.Any(words.Contains) ? AttachmentKind.Bill
        : InvoiceWords.Any(words.Contains) ? AttachmentKind.Invoice
        : AttachmentKind.Other;
}
