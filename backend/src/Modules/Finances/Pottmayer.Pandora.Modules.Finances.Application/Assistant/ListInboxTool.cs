using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPendingTransactions;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// <c>list_inbox</c>: what is waiting for review in the Finances inbox (<see cref="GetPendingTransactionsQuery"/>).
/// Read-only: approving or rejecting stays in the app, so the lines are bullets, not numbers.
/// </summary>
public sealed class ListInboxTool(ISender sender) : IAssistantTool
{
    private const int MaxShown = 20;

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "list_inbox",
        Description: "Shows the user what is waiting for review in their finances inbox (recorded expenses, imports, recurrences). Approving or rejecting is done in the app, not here. The list goes to the user directly; you will not see it.",
        ParametersJsonSchema: """{ "type": "object", "properties": {} }""",
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("what's in my finances inbox?", "{}"),
            new AssistantCommandExample("do I have anything to review?", "{}"),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments) =>
        context.Text("Mostrar a caixa de entrada do Finances?", "Show your Finances inbox?");

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetPendingTransactionsQuery(new GetPendingTransactionsInput(
            context.UserId, new PendingTransactionFilter(Take: 200))), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var pending = result.Value ?? [];
        var recap = $"[list_inbox: {pending.Count} item(s) shown to the user; content withheld from you]";
        if (pending.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text("A caixa de entrada do Finances está vazia.", "Your Finances inbox is empty."), recap);

        var accounts = (await sender.Send(new GetAccountsQuery(new GetAccountsInput(context.UserId, IncludeArchived: true)), ct)).Value ?? [];
        var cards = (await sender.Send(new GetCardsQuery(new GetCardsInput(context.UserId, IncludeArchived: true)), ct)).Value ?? [];
        var places = accounts.Select(a => (a.Id, a.Name)).Concat(cards.Select(c => (c.Id, c.Name))).ToDictionary(p => p.Id, p => p.Name);

        var sb = new StringBuilder(context.Text($"Caixa de entrada do Finances ({pending.Count}):", $"Finances inbox ({pending.Count}):"));
        foreach (var p in pending.OrderByDescending(p => p.OccurredOn).Take(MaxShown))
        {
            sb.Append("\n• ").Append(FinanceText.ShortDay(context, p.OccurredOn)).Append(" · ").Append(p.Description)
              .Append(" · ").Append(p.Amount is { } amount ? FinanceText.Money(context, amount, p.Currency) : "—");
            if ((p.CardId ?? p.AccountId) is { } placeId && places.GetValueOrDefault(placeId) is { } place)
                sb.Append(" · ").Append(place);
        }
        if (pending.Count > MaxShown)
            sb.Append('\n').Append(context.Text($"…e mais {pending.Count - MaxShown}.", $"…and {pending.Count - MaxShown} more."));
        sb.Append("\n\n").Append(context.Text("Aprove ou rejeite no Finances.", "Approve or reject them in Finances."));

        return AssistantCommandOutcome.Ok(sb.ToString(), recap);
    }
}
