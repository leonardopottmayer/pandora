using System.Globalization;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetSystemCategories;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransactions;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetUserCategories;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// The filter the transaction tools share — period, kind, account or card, category, text — as the model
/// passes it, run here over <see cref="GetTransactionsQuery"/>. The model names the account, the card and the
/// category in the user's words; they are matched here, so no name of the user's is sent to it. An account
/// and a card are asked for apart, since a card is often named after its bank's account ("Nubank"). Only
/// posted entries count — voided ones never happened and scheduled ones have not yet. Also hands back the
/// names the tools write the lines with.
/// </summary>
internal static class TransactionSearch
{
    /// <summary>The most entries one question reads: years of a household's expenses.</summary>
    private const int MaxScan = 5000;

    private const int PageSize = 200;

    public const string SchemaProperties = """
            "from": { "type": "string", "description": "First day of the period (bare date, 2026-09-01)." },
            "to": { "type": "string", "description": "Last day of the period, inclusive (bare date)." },
            "account": { "type": "string", "description": "The bank account as the user named it (e.g. \"Nubank\", \"Itaú\" — the name only, without \"conta\"), when they asked about one; not a credit card." },
            "card": { "type": "string", "description": "The credit card as the user named it (\"cartão Nubank\" → \"Nubank\" — the name only, without \"cartão\"), when they asked about one." },
            "category": { "type": "string", "description": "The category as the user named it (e.g. \"mercado\", \"restaurantes\"), when they asked about one." },
            "text": { "type": "string", "description": "A word to look for in the description or payee (e.g. \"ifood\", \"uber\"), when the user named a merchant or item." }
        """;

    public sealed record Filter(DateOnly? From, DateOnly? To, string? Kind, string? Account, string? Card, string? Category, string? Text);

    /// <summary>What a search found, with the names of the accounts, cards and categories involved.</summary>
    public sealed record Found(
        IReadOnlyList<TransactionDto> Items,
        bool Truncated,
        IReadOnlyDictionary<Guid, string> Places,
        IReadOnlyDictionary<Guid, string> Categories)
    {
        /// <summary>The card the entry was made on, else its account.</summary>
        public string? PlaceOf(TransactionDto t) =>
            (t.CardId is { } card ? Places.GetValueOrDefault(card) : null)
            ?? (t.AccountId is { } account ? Places.GetValueOrDefault(account) : null);

        /// <summary>The user's own category when set, else the system one.</summary>
        public string? CategoryOf(TransactionDto t) =>
            (t.UserCategoryId is { } user ? Categories.GetValueOrDefault(user) : null)
            ?? (t.SystemCategoryId is { } system ? Categories.GetValueOrDefault(system) : null);
    }

    public static Filter Parse(JsonElement arguments, string? defaultKind)
    {
        var from = Day(arguments, "from");
        var to = Day(arguments, "to");
        var kind = OptionalString(arguments, "kind") ?? defaultKind;
        if (kind is not (null or "expense" or "income"))
            throw new ArgumentException("The 'kind' argument must be 'expense' or 'income'.");
        return new Filter(
            from, to is { } t && from is { } f && t < f ? f : to, kind,
            OptionalString(arguments, "account"), OptionalString(arguments, "card"),
            OptionalString(arguments, "category"), OptionalString(arguments, "text"));
    }

    public static async Task<(Found? Found, string? Problem)> RunAsync(
        ISender sender, AssistantToolContext context, Filter filter, CancellationToken ct)
    {
        var accounts = (await sender.Send(new GetAccountsQuery(new GetAccountsInput(context.UserId, IncludeArchived: true)), ct)).Value ?? [];
        var cards = (await sender.Send(new GetCardsQuery(new GetCardsInput(context.UserId, IncludeArchived: true)), ct)).Value ?? [];
        var categories = await CategoriesAsync(sender, context, ct);

        Guid? accountId = null, cardId = null;
        if (filter.Account is { } accountName)
        {
            var (account, problem) = PickByTitle(context, accounts.Where(a => a.ArchivedAt is null), a => a.Name,
                accountName, "suas contas", "your accounts");
            if (account is null)
                return (null, problem);
            accountId = account.Id;
        }
        if (filter.Card is { } cardName)
        {
            var (card, problem) = PickByTitle(context, cards.Where(c => c.ArchivedAt is null), c => c.Name,
                cardName, "seus cartões", "your cards");
            if (card is null)
                return (null, problem);
            cardId = card.Id;
        }

        HashSet<Guid>? categoryIds = null;
        if (filter.Category is { } categoryName)
        {
            var (category, problem) = PickByTitle(context, categories, c => c.Name, categoryName, "suas categorias", "your categories");
            if (category is null)
                return (null, problem);
            categoryIds = [.. category.WithDescendants];
        }

        var items = new List<TransactionDto>();
        var truncated = false;
        for (var skip = 0; ; skip += PageSize)
        {
            var page = await sender.Send(new GetTransactionsQuery(new GetTransactionsInput(
                context.UserId, accountId, filter.From, filter.To, filter.Kind, Status: null,
                SystemCategoryId: null, UserCategoryId: null, filter.Text, Origin: null, TagIds: null,
                skip, PageSize)), ct);
            if (!page.IsSuccess)
                return (null, string.Join("; ", page.Errors.Select(e => e.Message)));

            var rows = page.Value ?? [];
            items.AddRange(rows);
            if (rows.Count < PageSize)
                break;
            if (items.Count >= MaxScan)
            {
                truncated = true;
                break;
            }
        }

        var matching = items
            .Where(t => t.Status == "posted")
            .Where(t => cardId is null || t.CardId == cardId)
            .Where(t => categoryIds is null
                || (t.UserCategoryId is { } u && categoryIds.Contains(u))
                || (t.SystemCategoryId is { } s && categoryIds.Contains(s)))
            .ToList();

        var places = accounts.Select(a => (a.Id, a.Name)).Concat(cards.Select(c => (c.Id, c.Name)))
            .ToDictionary(p => p.Id, p => p.Name);
        return (new Found(matching, truncated, places, categories.ToDictionary(c => c.Id, c => c.Name)), null);
    }

    /// <summary>"de 01/09/2026 a 30/09/2026", "desde 01/09/2026", "até 30/09/2026" or "" — the period in words.</summary>
    public static string Period(AssistantToolContext context, Filter filter) => (filter.From, filter.To) switch
    {
        ({ } f, { } t) when f == t => context.Text($" em {FinanceText.Day(context, f)}", $" on {FinanceText.Day(context, f)}"),
        ({ } f, { } t) => context.Text($" de {FinanceText.Day(context, f)} a {FinanceText.Day(context, t)}",
            $" from {FinanceText.Day(context, f)} to {FinanceText.Day(context, t)}"),
        ({ } f, null) => context.Text($" desde {FinanceText.Day(context, f)}", $" since {FinanceText.Day(context, f)}"),
        (null, { } t) => context.Text($" até {FinanceText.Day(context, t)}", $" until {FinanceText.Day(context, t)}"),
        _ => "",
    };

    private sealed record Category(Guid Id, string Name, IReadOnlyList<Guid> WithDescendants);

    /// <summary>Every system and user category, each with its own id and those of the categories under it.</summary>
    private static async Task<IReadOnlyList<Category>> CategoriesAsync(ISender sender, AssistantToolContext context, CancellationToken ct)
    {
        var system = (await sender.Send(new GetSystemCategoriesQuery(new GetSystemCategoriesInput(null, IncludeInactive: true)), ct)).Value ?? [];
        var user = (await sender.Send(new GetUserCategoriesQuery(new GetUserCategoriesInput(context.UserId, IncludeInactive: true)), ct)).Value ?? [];

        var all = new List<Category>();
        void AddSystem(SystemCategoryDto c)
        {
            all.Add(new Category(c.Id, c.Name, [c.Id, .. Flatten(c, x => x.Children).Select(x => x.Id)]));
            foreach (var child in c.Children)
                AddSystem(child);
        }
        void AddUser(UserCategoryDto c)
        {
            all.Add(new Category(c.Id, c.Name, [c.Id, .. Flatten(c, x => x.Children).Select(x => x.Id)]));
            foreach (var child in c.Children)
                AddUser(child);
        }
        foreach (var c in system)
            AddSystem(c);
        foreach (var c in user)
            AddUser(c);
        return all;
    }

    private static IEnumerable<T> Flatten<T>(T node, Func<T, IEnumerable<T>> children) =>
        children(node).SelectMany(c => Flatten(c, children).Prepend(c));

    private static DateOnly? Day(JsonElement arguments, string name) =>
        OptionalString(arguments, name) is { } text
            ? DateOnly.ParseExact(text[..Math.Min(10, text.Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
}
