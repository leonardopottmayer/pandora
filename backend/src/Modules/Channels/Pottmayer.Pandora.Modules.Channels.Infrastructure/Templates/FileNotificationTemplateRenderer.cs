using System.Collections.Concurrent;
using System.Reflection;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Templates;

/// <summary>
/// Renders from files embedded in this assembly, one per key, channel and locale. Files, not rows in
/// a table: the text is reviewed with the code that fills it, and a change to it shows up in a diff.
/// </summary>
/// <remarks>
/// An e-mail file puts the subject on the first line and the body on the rest. A channel with no
/// subject is the whole file, body only. Substitution is literal: <c>{{name}}</c> is replaced by
/// <c>payload["name"]</c>, and nothing else is interpreted.
/// </remarks>
public sealed class FileNotificationTemplateRenderer : INotificationTemplateRenderer
{
    private const string ResourcePrefix = "Pottmayer.Pandora.Modules.Channels.Infrastructure.Templates.Files.";

    private static readonly Assembly Assembly = typeof(FileNotificationTemplateRenderer).Assembly;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public NotificationContent Render(
        TemplateKey templateKey,
        Channel channel,
        string locale,
        IReadOnlyDictionary<string, string> payload)
    {
        var text = Substitute(Load(templateKey.Value, channel, locale), payload);

        // Only e-mail carries a subject, and it is the first line of the file.
        if (channel != Channel.Email)
            return new NotificationContent(Subject: string.Empty, Body: text.Trim(), IsHtml: false);

        var split = text.Split('\n', 2);
        var subject = split[0].Trim();
        var body = split.Length > 1 ? split[1].TrimStart('\n').Trim() : string.Empty;

        return new NotificationContent(subject, body, IsHtml: false);
    }

    /// <summary>
    /// Reads every declared variant. Called at startup so a missing file is a boot failure rather
    /// than a notification that silently never goes out.
    /// </summary>
    public static IReadOnlyList<string> FindMissingVariants()
    {
        var missing = new List<string>();

        foreach (var (key, channel, locale) in TemplateCatalog.AllVariants())
        {
            var name = ResourceName(key, channel, locale);
            using var stream = Assembly.GetManifestResourceStream(name);
            if (stream is null)
                missing.Add($"{key}/{channel.Value}.{locale}.txt");
        }

        return missing;
    }

    private string Load(string key, Channel channel, string locale)
    {
        var name = ResourceName(key, channel, locale);

        return _cache.GetOrAdd(name, static resourceName =>
        {
            using var stream = Assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                throw new InvalidOperationException($"No template resource '{resourceName}'.");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    private static string ResourceName(string key, Channel channel, string locale) =>
        $"{ResourcePrefix}{key}.{channel.Value}.{locale}.txt";

    private static string Substitute(string template, IReadOnlyDictionary<string, string> payload)
    {
        if (!template.Contains("{{", StringComparison.Ordinal))
            return template;

        var result = template;
        foreach (var (name, value) in payload)
            result = result.Replace($"{{{{{name}}}}}", value, StringComparison.Ordinal);

        return result;
    }
}
