using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Rendering;

/// <summary>
/// The structured content stored on a Telegram notification's <c>rendered_payload</c>: the text plus
/// the inline buttons, each carrying the id of its interaction row as its callback data. Written at
/// enqueue time and read by the transport, so the shape lives in one place.
/// </summary>
public sealed record TelegramRenderedPayload(string Text, IReadOnlyList<TelegramRenderedButton> Buttons)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public static TelegramRenderedPayload? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TelegramRenderedPayload>(json, Json);
}

/// <summary>One inline button: the id to route a tap back to, and the label to show.</summary>
public sealed record TelegramRenderedButton(string InteractionId, string Label);
