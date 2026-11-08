using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>The cover across the top of a page: a library image or one of the preset gradients.</summary>
public sealed record PageCover(Guid? MediaId, string? Gradient, int Position = 50);

/// <summary>
/// How a page looks (nte001.appearance): its cover, its width (<c>normal</c> or <c>wide</c>), its font
/// (<c>default</c>, <c>serif</c>, <c>mono</c>) and whether its text is small. Cosmetic: not part of the
/// page's versions.
/// </summary>
public sealed record PageAppearance(PageCover? Cover, string Width, string Font, bool SmallText)
{
    public static readonly string[] Widths = ["normal", "wide"];
    public static readonly string[] Fonts = ["default", "serif", "mono"];
    public static readonly string[] Gradients = ["sunset", "ocean", "forest", "dusk", "sand", "slate"];

    public static readonly PageAppearance Default = new(null, "normal", "default", false);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Guid? CoverMediaId => Cover?.MediaId;

    /// <summary>The appearance, or why it is not one: an unknown width or font, a cover with both or neither source.</summary>
    public static bool TryCreate(PageCover? cover, string? width, string? font, bool smallText, out PageAppearance appearance, out string? problem)
    {
        appearance = Default;
        problem = null;
        width ??= "normal";
        font ??= "default";
        if (!Widths.Contains(width)) problem = $"Unknown width '{width}'.";
        else if (!Fonts.Contains(font)) problem = $"Unknown font '{font}'.";
        else if (cover is not null && (cover.MediaId is null) == (cover.Gradient is null)) problem = "A cover is either a library image or a gradient.";
        else if (cover?.Gradient is { } gradient && !Gradients.Contains(gradient)) problem = $"Unknown gradient '{gradient}'.";
        else if (cover is not null && cover.Position is < 0 or > 100) problem = "The cover position goes from 0 to 100.";
        if (problem is not null) return false;
        appearance = new PageAppearance(cover, width, font, smallText);
        return true;
    }

    /// <summary>The stored JSON read back; anything unreadable is the default look.</summary>
    public static PageAppearance Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default;
        try
        {
            var read = JsonSerializer.Deserialize<PageAppearance>(json, Json);
            return read is null ? Default : read with { Width = read.Width ?? "normal", Font = read.Font ?? "default" };
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
