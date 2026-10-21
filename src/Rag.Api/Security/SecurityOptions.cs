namespace Rag.Api.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Hujjat boshqaruvi endpointlari uchun. Bo'sh bo'lsa tekshirilmaydi (faqat lokal ishlab chiqish uchun).</summary>
    public string? AdminApiKey { get; init; }

    public int ChatRequestsPerMinute { get; init; } = 20;
}
