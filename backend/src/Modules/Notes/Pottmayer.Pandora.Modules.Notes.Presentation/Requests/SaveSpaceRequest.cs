namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

/// <summary>A space's editable fields, for create and update. The color is a hex literal (<c>#7c3aed</c>).</summary>
public sealed record SaveSpaceRequest(string Name, string? Icon, string? Color, string? Description);
