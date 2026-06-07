using System.Globalization;
using System.Text;

namespace Pottmayer.Pandora.Modules.Identity.Application.Security;

/// <summary>
/// Central password strength policy, shared by the reset and change flows.
/// Rules: at least 8 characters, at least one uppercase letter, one digit and one special
/// character, and no emoji / non-printable symbols.
/// </summary>
internal static class PasswordPolicy
{
    private const int MinLength = 8;

    public static bool IsSatisfiedBy(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            return false;

        var hasUpper = false;
        var hasDigit = false;
        var hasSpecial = false;

        foreach (var rune in password.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);

            // Reject emoji and any non-printable / symbol-pictographic content.
            if (category is UnicodeCategory.OtherNotAssigned
                or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.Control
                or UnicodeCategory.Format)
                return false;

            if (rune.Value > 0x7F)
                return false;

            if (Rune.IsUpper(rune))
                hasUpper = true;
            else if (Rune.IsDigit(rune))
                hasDigit = true;
            else if (!Rune.IsLetterOrDigit(rune) && !Rune.IsWhiteSpace(rune))
                hasSpecial = true;
        }

        return hasUpper && hasDigit && hasSpecial;
    }
}
