using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

/// <summary>A page's look: <c>width</c> normal or wide, <c>font</c> default, serif or mono, and its cover (or none).</summary>
public sealed record SetPageAppearanceRequest(PageCover? Cover, string? Width, string? Font, bool SmallText);

public sealed record SetPageLockedRequest(bool Locked);

/// <summary><c>days</c> from 1 to 365, or null for a verification that does not run out; <c>verified: false</c> clears it.</summary>
public sealed record SetPageVerificationRequest(bool Verified, int? Days);

public sealed record SetPagePublicRequest(bool Public);
