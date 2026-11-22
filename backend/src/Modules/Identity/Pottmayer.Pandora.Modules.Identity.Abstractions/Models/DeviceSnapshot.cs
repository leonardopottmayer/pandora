namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Models;

/// <summary>A read-only view of a paired device. <see cref="Platform"/> is <c>windows</c>, <c>linux</c>, <c>macos</c>, <c>android</c> or <c>ios</c>.</summary>
public sealed record DeviceSnapshot(Guid Id, string Name, string Platform);
