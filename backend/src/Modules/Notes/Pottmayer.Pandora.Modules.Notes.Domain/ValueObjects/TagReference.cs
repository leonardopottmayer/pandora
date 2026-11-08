namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>A tag of a page's tags block: the name as written plus the key it normalizes to.</summary>
public sealed record TagReference(string Name, string Slug);
