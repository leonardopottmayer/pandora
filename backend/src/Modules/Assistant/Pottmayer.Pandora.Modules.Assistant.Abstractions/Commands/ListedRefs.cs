using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// Pointing at a listed item by number. The model passes <c>"ref": 2</c>; before the call runs (or is held
/// for confirmation) the pipeline <see cref="Pin"/>s the item that number stood for into the arguments, so
/// what is stored and later confirmed is that item — not whatever is number 2 by the time the user taps
/// Confirm. Tools read the pinned item through <see cref="ToolArguments.OptionalRef"/>.
/// </summary>
public static class ListedRefs
{
    public const string Ref = "ref";
    internal const string Id = "ref_id";
    internal const string Kind = "ref_kind";
    internal const string Label = "ref_label";
    internal const string At = "ref_at";

    /// <summary>
    /// The arguments with the item <c>ref</c> points at pinned in. Pinned fields the model sent itself are
    /// dropped first — only the pipeline pins. A number outside the list is left as is, for the tool to report.
    /// </summary>
    public static JsonElement Pin(JsonElement arguments, IReadOnlyList<ListedItem> listing)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
            return arguments;

        var node = JsonNode.Parse(arguments.GetRawText())!.AsObject();
        foreach (var name in new[] { Id, Kind, Label, At })
            node.Remove(name);

        if (Number(arguments) is { } n && n >= 1 && n <= listing.Count)
        {
            var item = listing[n - 1];
            node[Id] = item.Id.ToString();
            node[Kind] = item.Kind;
            node[Label] = item.Label;
            if (item.At is { } at)
                node[At] = at.ToString("O", CultureInfo.InvariantCulture);
        }

        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }

    /// <summary>
    /// The arguments with <paramref name="target"/> pinned in, as <see cref="Pin"/> would pin a listed item —
    /// for a target found by name (<see cref="IAssistantTargetedTool"/>). A <c>ref</c> already there is kept.
    /// </summary>
    public static JsonElement PinTarget(JsonElement arguments, ListedItem target)
    {
        var node = JsonNode.Parse(arguments.GetRawText())!.AsObject();
        node[Id] = target.Id.ToString();
        node[Kind] = target.Kind;
        node[Label] = target.Label;
        node.Remove(At);
        if (target.At is { } at)
            node[At] = at.ToString("O", CultureInfo.InvariantCulture);
        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }

    /// <summary>True when the arguments carry an item pinned by the pipeline (by number or by name).</summary>
    public static bool IsPinned(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(Id, out _);

    /// <summary>
    /// The number <c>ref</c> carries when <see cref="Pin"/> found nothing behind it (it is not on the last
    /// list, or there is none); null when there is no <c>ref</c> or it was pinned.
    /// </summary>
    public static int? Unpinned(JsonElement arguments) =>
        Number(arguments) is { } n && !arguments.TryGetProperty(Id, out _) ? n : null;

    /// <summary>What to tell the user when <paramref name="number"/> is not on the last list.</summary>
    public static string NotListed(AssistantToolContext context, int number) => context.Text(
        $"Não há item {number} na última lista. Peça a lista de novo.",
        $"There is no item {number} on the last list. Ask for the list again.");

    /// <summary>The number the model passed as <c>ref</c> (a JSON number or a numeric string), if any.</summary>
    public static int? Number(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(Ref, out var e)
            ? e.ValueKind switch
            {
                JsonValueKind.Number when e.TryGetInt32(out var n) => n,
                JsonValueKind.String when int.TryParse(e.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) => n,
                _ => null,
            }
            : null;

    /// <summary>
    /// The line the history keeps after a numbered list: each number's kind (never its content), and how to
    /// point at one, so the model picks the right tool for "cancela o 2".
    /// </summary>
    public static string Summary(IReadOnlyList<ListedItem> listing) =>
        $"[numbered for reference: {string.Join(", ", listing.Select((item, i) => $"{i + 1}={item.Kind}"))}; " +
        "to act on one, pass its number as \"ref\"]";
}
