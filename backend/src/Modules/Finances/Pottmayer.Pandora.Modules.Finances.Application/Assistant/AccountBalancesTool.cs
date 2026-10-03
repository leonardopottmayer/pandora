using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccountBalance;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// <c>account_balances</c>: the balance of each open account (<see cref="GetAccountBalanceQuery"/>) — what is
/// posted, and what it will be once the scheduled entries land when that differs — with the total per currency.
/// </summary>
public sealed class AccountBalancesTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "account_balances",
        Description: "Shows the user the balance of each of their accounts and the total. The numbers go to the user directly; you will not see them.",
        ParametersJsonSchema: """{ "type": "object", "properties": {} }""",
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("how much do I have in my accounts?", "{}"),
            new AssistantCommandExample("what's my balance?", "{}"),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments) =>
        context.Text("Mostrar os saldos das contas?", "Show your account balances?");

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var accounts = (await sender.Send(new GetAccountsQuery(new GetAccountsInput(context.UserId, IncludeArchived: false)), ct)).Value ?? [];
        var recap = $"[account_balances: {accounts.Count} account(s) shown to the user; content withheld from you]";
        if (accounts.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text("Você ainda não tem contas no Finances.", "You don't have accounts in Finances yet."), recap);

        var sb = new StringBuilder(context.Text("Saldos:", "Balances:"));
        var totals = new Dictionary<string, decimal>();
        foreach (var account in accounts.OrderBy(a => a.DisplayOrder))
        {
            var balance = await sender.Send(new GetAccountBalanceQuery(new GetAccountBalanceInput(context.UserId, account.Id)), ct);
            if (!balance.IsSuccess)
                return AssistantCommandOutcome.Failed(string.Join("; ", balance.Errors.Select(e => e.Message)));

            var b = balance.Value!;
            sb.Append("\n• ").Append(account.Name).Append(" — ").Append(FinanceText.Money(context, b.Posted, b.Currency));
            if (b.Projected != b.Posted)
                sb.Append(context.Text(" (previsto ", " (projected ")).Append(FinanceText.Money(context, b.Projected, b.Currency)).Append(')');
            totals[b.Currency] = totals.GetValueOrDefault(b.Currency) + b.Posted;
        }

        foreach (var (currency, total) in totals)
            sb.Append(totals.Count == 1 ? "\n\nTotal: " : $"\nTotal {currency}: ").Append(FinanceText.Money(context, total, currency));

        return AssistantCommandOutcome.Ok(sb.ToString(), recap);
    }
}
