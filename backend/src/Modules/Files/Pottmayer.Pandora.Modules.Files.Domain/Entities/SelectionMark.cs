using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Domain.Entities;

/// <summary>One mark of a root's selection tree: a folder (<c>/</c> is the root) in or out, down to a deeper mark.</summary>
public sealed class SelectionMark
{
    public Guid Id { get; private set; }
    public Guid RootId { get; private set; }
    public string Path { get; private set; } = string.Empty;
    public SelectionMode Mode { get; private set; } = null!;

    private SelectionMark() { }

    internal static SelectionMark Create(Guid rootId, string path, SelectionMode mode)
        => new() { Id = Guid.CreateVersion7(), RootId = rootId, Path = path, Mode = mode };
}
