using System.Text.RegularExpressions;
using Rag.Application.Abstractions;

namespace Rag.Infrastructure.Pdf;

/// <summary>Har sahifada takrorlanadigan kolontitul va sahifa raqamlarini olib tashlaydi.</summary>
internal static partial class HeaderFooterFilter
{
    private const int EdgeLines = 2;
    private const int MinPages = 3;
    private const double RepeatRatio = 0.6;

    public static IReadOnlyList<PdfPage> Apply(IReadOnlyList<PdfPage> pages)
    {
        var repeated = pages.Count >= MinPages ? FindRepeated(pages) : new HashSet<string>();

        return pages
            .Select(page => page with
            {
                Lines = page.Lines
                    .Where((line, index) => !IsEdge(index, page.Lines.Count)
                                            || !(repeated.Contains(Key(line.Text)) || PageNumber().IsMatch(line.Text)))
                    .ToList()
            })
            .ToList();
    }

    private static HashSet<string> FindRepeated(IReadOnlyList<PdfPage> pages)
    {
        var threshold = pages.Count * RepeatRatio;

        return pages
            .SelectMany(page => page.Lines
                .Where((_, index) => IsEdge(index, page.Lines.Count))
                .Select(line => Key(line.Text))
                .Distinct())
            .GroupBy(key => key)
            .Where(group => group.Count() >= threshold)
            .Select(group => group.Key)
            .ToHashSet();
    }

    private static bool IsEdge(int index, int count) => index < EdgeLines || index >= count - EdgeLines;

    // Sahifa raqami har sahifada boshqacha — raqamlarsiz solishtiriladi.
    private static string Key(string text) => Digits().Replace(text, "#");

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^[\s\-–—]*\d+(\s*(/|из|of|dan)\s*\d+)?[\s\-–—]*$", RegexOptions.IgnoreCase)]
    private static partial Regex PageNumber();
}
