using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.Entities;

/// <summary>
/// A name rule on top of a root's selection. Its scope is the most specific of <see cref="ScopePath"/>
/// (a folder of <see cref="RootId"/>), <see cref="RootId"/>, <see cref="DeviceId"/>, or the whole user.
/// Built-in filters are ordinary ones the user starts with: editable, disableable, deletable.
/// </summary>
public sealed class Filter : IAuditable
{
    public const int NameMaxLength = 100;
    public const int PatternMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public Guid? RootId { get; private set; }
    public string? ScopePath { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public FilterAction Action { get; private set; } = null!;
    public FilterTarget AppliesTo { get; private set; } = null!;
    public FilterMatcher Matcher { get; private set; } = null!;
    public string Pattern { get; private set; } = string.Empty;

    /// <summary>Null follows the root's setting.</summary>
    public bool? CaseSensitive { get; private set; }

    public bool IsEnabled { get; private set; }
    public bool IsBuiltin { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Filter() { }

    /// <summary>Callers validate the definition first (names, pattern, scope ownership).</summary>
    public static Filter Create(Guid userId, FilterDefinition definition) =>
        Apply(new Filter { Id = Guid.CreateVersion7(), UserId = userId }, definition);

    public void Update(FilterDefinition definition) => Apply(this, definition);

    /// <summary>The system clutter a new user starts without: thumbnails caches, recycle bins, partial downloads.</summary>
    public static IEnumerable<Filter> CreateBuiltins(Guid userId)
    {
        var files = new[] { "Thumbs.db", "desktop.ini", ".DS_Store", "*.tmp", "*.part" };
        var folders = new[] { "$RECYCLE.BIN", "System Volume Information" };

        return files.Select(p => (p, FilterTarget.File))
            .Concat(folders.Select(p => (p, FilterTarget.Folder)))
            .Select(x =>
            {
                var filter = Create(userId, new FilterDefinition(
                    null, null, null, x.p, FilterAction.Exclude, x.Item2, FilterMatcher.Glob, x.p, false, true));
                filter.IsBuiltin = true;
                return filter;
            });
    }

    private static Filter Apply(Filter filter, FilterDefinition d)
    {
        filter.DeviceId = d.DeviceId;
        filter.RootId = d.RootId;
        filter.ScopePath = d.ScopePath;
        filter.Name = d.Name.Trim();
        filter.Action = d.Action;
        filter.AppliesTo = d.AppliesTo;
        filter.Matcher = d.Matcher;
        filter.Pattern = d.Pattern;
        filter.CaseSensitive = d.CaseSensitive;
        filter.IsEnabled = d.IsEnabled;
        return filter;
    }
}

/// <summary>Everything about a filter the user edits.</summary>
public sealed record FilterDefinition(
    Guid? DeviceId,
    Guid? RootId,
    string? ScopePath,
    string Name,
    FilterAction Action,
    FilterTarget AppliesTo,
    FilterMatcher Matcher,
    string Pattern,
    bool? CaseSensitive,
    bool IsEnabled);
