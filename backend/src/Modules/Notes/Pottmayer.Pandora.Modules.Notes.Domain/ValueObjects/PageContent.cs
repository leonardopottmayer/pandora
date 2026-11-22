using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>
/// A page's body: a document of blocks (<c>{ "version": 1, "blocks": [...] }</c>). The shape is checked
/// here; what each block type's props mean belongs to the editor, so a new block type needs nothing
/// from the backend unless it carries derived data. What the backend derives from it — search text,
/// page links, tags, media use — is read here once, on parse.
/// </summary>
public sealed partial class PageContent
{
    public const int CurrentVersion = 1;
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxBlocks = 10_000;
    public const int MaxDepth = 24;

    public static readonly PageContent Empty = Build(new JsonArray());

    private PageContent(string json, JsonArray blocks)
    {
        Json = json;
        Blocks = blocks;
        var walk = new Walk();
        walk.Blocks(blocks);
        Text = walk.Text.ToString().Trim();
        MentionedPageIds = walk.Mentions;
        LinkedPageIds = walk.PageLinks;
        Tags = walk.Tags;
        MediaIds = walk.Media;
    }

    /// <summary>The document as stored.</summary>
    public string Json { get; }

    private JsonArray Blocks { get; }

    /// <summary>All the text a reader sees, for the search vector and excerpts.</summary>
    public string Text { get; }

    /// <summary>Pages named inside the text (inline mentions).</summary>
    public IReadOnlySet<Guid> MentionedPageIds { get; }

    /// <summary>Pages shown as a link block (a card to another page).</summary>
    public IReadOnlySet<Guid> LinkedPageIds { get; }

    /// <summary>The tags of the page's tags block, as written, one per distinct slug.</summary>
    public IReadOnlyList<TagReference> Tags { get; }

    /// <summary>Library items any block shows (<c>props.mediaId</c>, at any depth of the props).</summary>
    public IReadOnlySet<Guid> MediaIds { get; }

    public bool IsEmpty => Blocks.Count == 0;

    /// <summary>Properties at any block depth, in document order; unnamed rows are ignored.</summary>
    public IReadOnlyList<PageProperty> Properties()
    {
        var result = new List<PageProperty>();
        static string Plain(JsonNode? node)
        {
            var walk = new Walk();
            walk.Inlines(node as JsonArray);
            return walk.Text.ToString().Trim();
        }
        void Collect(JsonArray blocks)
        {
            foreach (var block in blocks.OfType<JsonObject>())
            {
                if ((string?)block["type"] == "properties" && block["props"] is JsonObject props && props["rows"] is JsonArray rows)
                    foreach (var row in rows.OfType<JsonObject>())
                    {
                        var key = Plain(row["key"]);
                        if (key.Length > 0)
                            result.Add(new PageProperty(key, Plain(row["value"])));
                    }
                if (block["children"] is JsonArray children)
                    Collect(children);
            }
        }
        Collect(Blocks);
        return result;
    }

    /// <summary>
    /// Every block's id, at any depth, with a fingerprint of the block without its children, so an
    /// edit inside a list item does not count as an edit of the list.
    /// </summary>
    public IReadOnlyDictionary<string, string> BlockFingerprints()
    {
        var result = new Dictionary<string, string>();
        void Add(JsonArray blocks)
        {
            foreach (var node in blocks.OfType<JsonObject>())
            {
                var own = new JsonObject();
                foreach (var (key, value) in node)
                    if (key != "children")
                        own[key] = value?.DeepClone();
                var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(own.ToJsonString()));
                result[node["id"]!.GetValue<string>()] = Convert.ToHexString(hash, 0, 16);
                if (node["children"] is JsonArray children)
                    Add(children);
            }
        }
        Add(Blocks);
        return result;
    }

    /// <summary>The document, or the reason it is not one.</summary>
    public static bool TryParse(string? json, out PageContent content, out string? problem)
    {
        content = Empty;
        problem = null;

        if (string.IsNullOrWhiteSpace(json))
            return true;

        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            problem = "The page is too large.";
            return false;
        }

        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { problem = "The page content is not valid JSON."; return false; }

        if (root is not JsonObject obj || obj["blocks"] is not JsonArray blocks)
        {
            problem = "The page content needs a blocks list.";
            return false;
        }

        var ids = new HashSet<string>();
        var count = 0;
        problem = Check(blocks, 1, ids, ref count);
        if (problem is not null)
            return false;

        // Detached from the parsed root, so the normalized document can adopt it.
        obj.Remove("blocks");
        content = Build(blocks);
        return true;
    }

    /// <summary>For content that has passed validation before (a stored page or version).</summary>
    public static PageContent Parse(string? json) =>
        TryParse(json, out var content, out var problem)
            ? content
            : throw new FormatException(problem);

    /// <summary>
    /// Blocks from plain text, for what arrives as text (the Assistant): one block per line, with the
    /// common markdown line starts read as headings, lists, to-dos and quotes.
    /// </summary>
    public static PageContent FromText(string? text)
    {
        var blocks = new JsonArray();
        foreach (var raw in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0)
                continue;
            blocks.Add(LineBlock(line.TrimStart()));
        }
        return Build(blocks);
    }

    /// <summary>This document with <paramref name="other"/>'s blocks after its own.</summary>
    public PageContent Append(PageContent other)
    {
        var blocks = (JsonArray)Blocks.DeepClone();
        foreach (var block in other.Blocks)
            blocks.Add(block!.DeepClone());
        return Build(blocks);
    }

    /// <summary>A template's independent blocks, with text variables expanded and comment anchors removed.</summary>
    public PageContent InstantiateTemplate(string title, DateOnly date)
    {
        var blocks = (JsonArray)Blocks.DeepClone();
        var dateText = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        string Expand(string text) => text.Replace("{{date}}", dateText).Replace("{{title}}", title);

        void Rewrite(JsonNode? node)
        {
            if (node is JsonArray array)
            {
                foreach (var child in array)
                    Rewrite(child);
            }
            else if (node is JsonObject obj)
            {
                // Only visible text changes: URLs, media ids and references keep their meaning.
                foreach (var (key, value) in obj.ToList())
                {
                    if (key is "text" or "label" or "title" or "prompt" && value is JsonValue scalar && scalar.TryGetValue<string>(out var text))
                        obj[key] = Expand(text);
                    else if (key == "marks" && value is JsonArray marks)
                    {
                        for (var i = marks.Count - 1; i >= 0; i--)
                            if (marks[i]?["type"]?.GetValue<string>() == "comment")
                                marks.RemoveAt(i);
                    }
                    else
                        Rewrite(value);
                }
            }
        }

        void FreshIds(JsonArray items)
        {
            foreach (var block in items.OfType<JsonObject>())
            {
                block["id"] = NewId();
                if (block["children"] is JsonArray children)
                    FreshIds(children);
            }
        }

        FreshIds(blocks);
        Rewrite(blocks);
        return Build(blocks);
    }

    /// <summary>
    /// The page as readable text, one line per block, lists and headings marked the markdown way —
    /// what the Assistant sends a user who asked to read a note.
    /// </summary>
    public string ToReadableText()
    {
        var sb = new StringBuilder();
        Readable(Blocks, 0, sb);
        return sb.ToString().TrimEnd();
    }

    private static PageContent Build(JsonArray blocks)
    {
        var doc = new JsonObject { ["version"] = CurrentVersion, ["blocks"] = blocks };
        return new PageContent(doc.ToJsonString(), blocks);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,39}$")]
    private static partial Regex TypeRegex();

    private static string? Check(JsonArray blocks, int depth, HashSet<string> ids, ref int count)
    {
        if (depth > MaxDepth)
            return "The page nests blocks too deep.";

        foreach (var node in blocks)
        {
            if (node is not JsonObject block)
                return "Every block must be an object.";
            if (++count > MaxBlocks)
                return "The page has too many blocks.";

            if (block["id"] is not JsonValue idValue || !idValue.TryGetValue<string>(out var id) || id.Length is 0 or > 64)
                return "Every block needs an id.";
            if (!ids.Add(id))
                return $"The block id \"{id}\" appears twice.";

            if (block["type"] is not JsonValue typeValue || !typeValue.TryGetValue<string>(out var type) || !TypeRegex().IsMatch(type))
                return $"The block \"{id}\" has no valid type.";

            if (block["props"] is { } props && props is not JsonObject)
                return $"The props of block \"{id}\" must be an object.";

            if (block["content"] is { } content)
            {
                if (content is not JsonArray inlines || inlines.Any(i => i is not JsonObject))
                    return $"The content of block \"{id}\" must be a list of objects.";
            }

            if (block["children"] is { } children)
            {
                if (children is not JsonArray childBlocks)
                    return $"The children of block \"{id}\" must be a list.";
                var problem = Check(childBlocks, depth + 1, ids, ref count);
                if (problem is not null)
                    return problem;
            }
        }

        return null;
    }

    private static JsonObject LineBlock(string line)
    {
        (string Type, string Text, JsonObject? Props) parsed = line switch
        {
            _ when line.StartsWith("### ") => ("heading", line[4..], new JsonObject { ["level"] = 3 }),
            _ when line.StartsWith("## ") => ("heading", line[3..], new JsonObject { ["level"] = 2 }),
            _ when line.StartsWith("# ") => ("heading", line[2..], new JsonObject { ["level"] = 1 }),
            _ when line.StartsWith("- [ ] ") || line.StartsWith("[ ] ") => ("todo", line[(line.IndexOf(']') + 2)..], new JsonObject { ["checked"] = false }),
            _ when line.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase)
                => ("todo", line[(line.IndexOf(']') + 2)..], new JsonObject { ["checked"] = true }),
            _ when line.StartsWith("- ") || line.StartsWith("* ") => ("bulleted", line[2..], null),
            _ when NumberedRegex().Match(line) is { Success: true } m => ("numbered", line[m.Length..], null),
            _ when line.StartsWith("> ") => ("quote", line[2..], null),
            _ => ("paragraph", line, null),
        };

        var block = new JsonObject
        {
            ["id"] = NewId(),
            ["type"] = parsed.Type,
            ["content"] = new JsonArray(new JsonObject { ["text"] = parsed.Text })
        };
        if (parsed.Props is not null)
            block["props"] = parsed.Props;
        return block;
    }

    [GeneratedRegex(@"^\d+[.)] ")]
    private static partial Regex NumberedRegex();

    /// <summary>A short random id, the same shape the editor gives its blocks.</summary>
    private static string NewId() => Guid.NewGuid().ToString("N")[..12];

    private static void Readable(JsonArray blocks, int depth, StringBuilder sb)
    {
        var number = 0;
        foreach (var node in blocks)
        {
            if (node is not JsonObject block)
                continue;
            var type = (string?)block["type"];
            var props = block["props"] as JsonObject;
            var text = InlineText(block["content"] as JsonArray);
            var indent = new string(' ', depth * 2);
            number = type == "numbered" ? number + 1 : 0;

            var line = type switch
            {
                "heading" => $"{new string('#', Math.Clamp((int?)props?["level"] ?? 1, 1, 3))} {text}",
                "bulleted" => $"{indent}- {text}",
                "numbered" => $"{indent}{number}. {text}",
                "todo" => $"{indent}- [{((bool?)props?["checked"] == true ? "x" : " ")}] {text}",
                "quote" => $"> {text}",
                "code" => $"```\n{text}\n```",
                "ai" => InlineText(props?["output"] as JsonArray),
                "divider" => "---",
                "tags" => props?["tags"] is JsonArray tags ? string.Join(' ', tags.Select(t => $"#{t}")) : string.Empty,
                _ => $"{indent}{text}",
            };

            if (line.Trim().Length > 0)
                sb.Append(line).Append('\n');

            if (block["children"] is JsonArray children)
                Readable(children, depth + 1, sb);
        }
    }

    /// <summary>The text of an inline list; a mention reads as nothing (it names a page by id).</summary>
    private static string InlineText(JsonArray? inlines)
    {
        if (inlines is null)
            return string.Empty;
        var sb = new StringBuilder();
        foreach (var inline in inlines)
            if (inline is JsonObject obj && obj["text"] is JsonValue v && v.TryGetValue<string>(out var s))
                sb.Append(s);
        return sb.ToString();
    }

    /// <summary>One pass over the blocks, collecting what the backend derives.</summary>
    private sealed class Walk
    {
        public StringBuilder Text { get; } = new();
        public HashSet<Guid> Mentions { get; } = [];
        public HashSet<Guid> PageLinks { get; } = [];
        public HashSet<Guid> Media { get; } = [];
        public List<TagReference> Tags { get; } = [];
        private readonly HashSet<string> _tagSlugs = [];

        public void Blocks(JsonArray blocks)
        {
            foreach (var node in blocks)
            {
                if (node is not JsonObject block)
                    continue;
                var type = (string?)block["type"];
                var props = block["props"] as JsonObject;

                Inlines(block["content"] as JsonArray);

                if (props is not null)
                {
                    if (type == "tags" && props["tags"] is JsonArray tags)
                        foreach (var tag in tags)
                            AddTag(tag is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : null);

                    // A page-link block, and a synced block's copy, which shows a block of that page.
                    if (type is "page-link" or "synced-ref" && GuidOf(props["pageId"]) is { } linked)
                        PageLinks.Add(linked);

                    Props(props);
                }

                if (block["children"] is JsonArray children)
                    Blocks(children);
            }
        }

        public void Inlines(JsonArray? inlines)
        {
            if (inlines is null)
                return;
            foreach (var node in inlines)
            {
                if (node is not JsonObject inline)
                    continue;
                if (inline["text"] is JsonValue v && v.TryGetValue<string>(out var s))
                    Text.Append(s);
                if (inline["mention"] is JsonObject mention && GuidOf(mention["page"]) is { } page)
                    Mentions.Add(page);
                // The other chips read as their words: a status, another module's item, a formula, a date.
                if (ChipText(inline) is { } chip)
                    Text.Append(chip);
            }
            Text.Append('\n');
        }

        private static string? ChipText(JsonObject inline) =>
            (inline["status"] as JsonObject)?["label"] is JsonValue status && status.TryGetValue<string>(out var label) ? label
            : (inline["item"] as JsonObject)?["label"] is JsonValue item && item.TryGetValue<string>(out var name) ? name
            : (inline["math"] as JsonObject)?["latex"] is JsonValue math && math.TryGetValue<string>(out var latex) ? latex
            : (inline["date"] as JsonObject)?["value"] is JsonValue date && date.TryGetValue<string>(out var day) ? day
            : null;

        /// <summary>Media ids anywhere in the props, and text kept there (table cells, captions).</summary>
        private void Props(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (key, value) in obj)
                    {
                        if (key == "mediaId" && GuidOf(value) is { } media)
                            Media.Add(media);
                        else
                            Props(value);
                    }
                    break;
                // A list of inline objects is rich text (a table cell, a caption, a property's value), chips included.
                case JsonArray array when array.Count > 0 && array.All(i => i is JsonObject o && (o.ContainsKey("text") || o.ContainsKey("mention") || ChipText(o) is not null)):
                    Inlines(array);
                    break;
                case JsonArray array:
                    foreach (var item in array)
                        Props(item);
                    break;
            }
        }

        private void AddTag(string? raw)
        {
            var slug = TagName.ToSlug(raw);
            if (slug.Length > 0 && _tagSlugs.Add(slug))
                Tags.Add(new TagReference(TagName.ToDisplayName(raw!), slug));
        }

        private static Guid? GuidOf(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<string>(out var s) && Guid.TryParse(s, out var g) ? g : null;
    }
}
