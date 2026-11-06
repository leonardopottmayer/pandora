namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

/// <summary>A declared flag and its state. <see cref="Enabled"/> is off whenever an ancestor is off.</summary>
/// <param name="Override">The administrator's choice, or <c>null</c> while the default applies.</param>
public sealed record FeatureDto(string Key, string? Parent, bool DefaultEnabled, bool? Override, bool Enabled);
