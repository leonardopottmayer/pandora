using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>The checks a space's editable fields pass, shared by create and update.</summary>
internal static partial class SpaceValidation
{
    /// <summary>The first problem with a name and color, or <c>null</c>.</summary>
    public static Error? Check(string? name, string? color)
    {
        if (string.IsNullOrWhiteSpace(name))
            return SpaceErrors.InvalidName;

        // The color ends up inline in the frontend's styles, so only a hex literal is accepted.
        if (!string.IsNullOrWhiteSpace(color) && !HexColorRegex().IsMatch(color.Trim()))
            return SpaceErrors.InvalidColor;

        return null;
    }

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();
}
