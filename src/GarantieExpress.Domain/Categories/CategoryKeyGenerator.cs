using System.Globalization;
using System.Text;

namespace GarantieExpress.Domain;

public static class CategoryKeyGenerator
{
    public static string Generate(string? name, IEnumerable<string> existingKeys)
    {
        ArgumentNullException.ThrowIfNull(existingKeys);

        var normalized = (name ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0) builder.Append('-');
                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var root = builder.Length == 0 ? "category" : builder.ToString()[..Math.Min(builder.Length, 48)];
        var used = existingKeys.ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(root)) return root;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{root}-{suffix}";
            if (!used.Contains(candidate)) return candidate;
        }
    }
}
