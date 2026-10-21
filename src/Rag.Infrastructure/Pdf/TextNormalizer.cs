using System.Text;
using System.Text.RegularExpressions;

namespace Rag.Infrastructure.Pdf;

internal static partial class TextNormalizer
{
    /// <summary>NFC, "aqlli" tirnoqlar → oddiy (aks holda JSON buziladi), ortiqcha bo'shliqlar.</summary>
    public static string NormalizeLine(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormC)
            .Replace('“', '"').Replace('”', '"').Replace('„', '"').Replace('‟', '"')
            .Replace('‘', '\'').Replace('’', '\'')
            .Replace(' ', ' ')
            .Replace("­", string.Empty);

        return MultipleSpaces().Replace(normalized, " ").Trim();
    }

    /// <summary>Qatorni paragrafga qo'shadi; defis bilan uzilgan so'zni birlashtiradi.</summary>
    public static string AppendLine(string paragraph, string line)
    {
        if (paragraph.Length == 0)
            return line;

        var hyphenated = paragraph.Length > 1 && paragraph[^1] == '-' && char.IsLetter(paragraph[^2])
                         && line.Length > 0 && char.IsLower(line[0]);

        return hyphenated ? paragraph[..^1] + line : paragraph + " " + line;
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();
}
