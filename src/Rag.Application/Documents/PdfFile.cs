using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Rag.Application.Common.Exceptions;

namespace Rag.Application.Documents;

/// <summary>Yuklangan PDF: validatsiyadan o'tgan, xotiraga o'qilgan, SHA-256 hisoblangan.</summary>
public sealed partial class PdfFile
{
    public const string ContentType = "application/pdf";
    private static readonly byte[] MagicBytes = "%PDF-"u8.ToArray();

    private PdfFile(string fileName, byte[] content, string hash)
    {
        FileName = fileName;
        Content = content;
        Hash = hash;
    }

    public string FileName { get; }
    public byte[] Content { get; }
    public string Hash { get; }
    public long Size => Content.LongLength;

    public static async Task<PdfFile> ReadAsync(Stream stream, string fileName, int maxSizeMb, CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Faqat PDF fayl qabul qilinadi.");

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();

        if (content.Length == 0)
            throw new ValidationException("Fayl bo'sh.");
        if (content.LongLength > maxSizeMb * 1024L * 1024L)
            throw new ValidationException($"Fayl hajmi {maxSizeMb} MB dan oshmasligi kerak.");
        if (!content.AsSpan().StartsWith(MagicBytes))
            throw new ValidationException("Fayl haqiqiy PDF emas.");

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new PdfFile(SanitizeFileName(fileName), content, hash);
    }

    public static string StorageKey(Guid documentId, int version, string fileName) =>
        $"{documentId}/v{version}/{fileName}";

    private static string SanitizeFileName(string fileName)
    {
        var name = UnsafeChars().Replace(Path.GetFileName(fileName), "_");
        return name.Length <= 150 ? name : name[^150..];
    }

    [GeneratedRegex(@"[^\p{L}\p{N}._-]")]
    private static partial Regex UnsafeChars();
}
