using System.Globalization;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreatePendingExpense;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// <c>record_expense</c>: "gastei 45 no mercado no Nubank" lands in the Finances inbox as a manual
/// pending expense (<see cref="CreatePendingExpenseCommand"/>) — never a posted transaction; the user
/// approves it there. The model passes only the words naming the card or account; it is found here among
/// the user's open ones, so no account name is sent to the model. With nothing named and a single
/// account/card, that one is used; otherwise the reply asks which.
/// </summary>
public sealed class RecordExpenseTool(ISender sender, TimeProvider timeProvider) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "record_expense",
        Description: "Records money the user spent, for review in their finances inbox. You cannot see their accounts or cards: pass the words they used to name one. Not for paying a card statement or settling anything already in Finances — that is done in the app, and recording it here would count it twice; never offer it.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "amount": { "type": "number", "description": "How much was spent, as a positive number (e.g. 45.9)." },
            "description": { "type": "string", "description": "What it was, in a few words (e.g. \"Mercado\", \"Uber\")." },
            "card": { "type": "string", "description": "The credit card as the user named it, when paid by card (\"no cartão Nubank\", \"no crédito\"). Omit otherwise." },
            "account": { "type": "string", "description": "The account as the user named it, when paid from an account (pix, debit, cash). Omit otherwise." },
            "date": { "type": "string", "description": "The day it happened (2026-09-05), only when it was not today." }
          },
          "required": ["amount", "description"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "spent 45.90 at the supermarket on the Nubank card",
                """{ "amount": 45.9, "description": "Supermarket", "card": "Nubank" }"""),
            new AssistantCommandExample(
                "paid 30 for an Uber yesterday with pix from Itaú",
                """{ "amount": 30, "description": "Uber", "account": "Itaú", "date": "2026-09-03" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var e = Parse(context, arguments);
        var from = e.Card ?? e.Account;
        var amount = e.Amount.ToString("N2", FinanceText.Culture(context));
        return from is null
            ? context.Text($"Registrar a despesa \"{e.Description}\" de {amount}?", $"Record the expense \"{e.Description}\" of {amount}?")
            : context.Text($"Registrar a despesa \"{e.Description}\" de {amount} ({from})?", $"Record the expense \"{e.Description}\" of {amount} ({from})?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var e = Parse(context, arguments);

        var accounts = (await sender.Send(new GetAccountsQuery(new GetAccountsInput(context.UserId, IncludeArchived: false)), ct)).Value ?? [];
        var cards = (await sender.Send(new GetCardsQuery(new GetCardsInput(context.UserId, IncludeArchived: false)), ct)).Value ?? [];

        var target = Resolve(context, e, [.. accounts.Select(a => new Target(a.Id, a.Name, IsCard: false))],
            [.. cards.Select(c => new Target(c.Id, c.Name, IsCard: true))]);
        if (target.Problem is not null)
            return AssistantCommandOutcome.Failed(target.Problem);

        var chosen = target.Match!;
        var result = await sender.Send(new CreatePendingExpenseCommand(new CreatePendingExpenseInput(
            context.UserId,
            chosen.IsCard ? null : chosen.Id,
            chosen.IsCard ? chosen.Id : null,
            e.Amount, e.Date, e.Description)), ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(x => x.Message)));

        var pending = result.Value!;
        var money = FinanceText.Money(context, pending.Amount!.Value, pending.Currency);
        var day = FinanceText.Day(context, pending.OccurredOn);
        return AssistantCommandOutcome.Ok(context.Text(
            $"Despesa \"{pending.Description}\" de {money} ({chosen.Name}, {day}) enviada para a caixa de entrada do Finances.",
            $"Expense \"{pending.Description}\" of {money} ({chosen.Name}, {day}) sent to your Finances inbox."));
    }

    private sealed record Target(Guid Id, string Name, bool IsCard);

    private sealed record Args(decimal Amount, string Description, string? Card, string? Account, DateOnly Date);

    /// <summary>The card or account the user named, or the only one they have; otherwise why not.</summary>
    private static (Target? Match, string? Problem) Resolve(
        AssistantToolContext context, Args e, IReadOnlyList<Target> accounts, IReadOnlyList<Target> cards)
    {
        if (e.Card is { } card)
            return PickByTitle(context, cards, t => t.Name, card, "seus cartões", "your cards");
        if (e.Account is { } account)
            return PickByTitle(context, accounts, t => t.Name, account, "suas contas", "your accounts");

        IReadOnlyList<Target> all = [.. accounts, .. cards];
        if (all.Count == 1)
            return (all[0], null);
        if (all.Count == 0)
            return (null, context.Text(
                "Você ainda não tem conta nem cartão no Finances. Crie um primeiro.",
                "You don't have an account or card in Finances yet. Create one first."));

        var names = string.Join(", ", all.Take(8).Select(t => $"\"{t.Name}\""));
        return (null, context.Text(
            $"Pago com qual conta ou cartão? {names}",
            $"Paid with which account or card? {names}"));
    }

    private Args Parse(AssistantToolContext context, JsonElement arguments)
    {
        if (!arguments.TryGetProperty("amount", out var a) || !TryAmount(a, out var amount) || amount <= 0)
            throw new ArgumentException("The 'amount' argument must be a positive number.");

        var date = OptionalString(arguments, "date") is { } text
            ? DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), context.TimeZone).DateTime);

        return new Args(
            amount, RequiredString(arguments, "description"),
            OptionalString(arguments, "card"), OptionalString(arguments, "account"), date);
    }

    // The model sometimes sends the number as a string ("45,90"); accept both separators.
    private static bool TryAmount(JsonElement element, out decimal amount)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetDecimal(out amount);

        amount = 0;
        return element.ValueKind == JsonValueKind.String
            && decimal.TryParse(element.GetString()!.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}
