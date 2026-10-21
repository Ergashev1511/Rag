using Rag.Domain.Common;

namespace Rag.Domain.Entities;

public sealed class Document : BaseEntity
{
    private readonly List<DocumentVersion> _versions = [];

    private Document() { }

    public Document(string title, string? description)
    {
        SetInfo(title, description);
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>Qidiruvda ishlatiladigan faol versiya (0 = hali indekslanmagan).</summary>
    public int CurrentVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public IReadOnlyCollection<DocumentVersion> Versions => _versions;

    public bool IsDeleted => DeletedAt is not null;

    public int NextVersionNumber => _versions.Count == 0 ? 1 : _versions.Max(v => v.Version) + 1;

    public DocumentVersion? LatestVersion => _versions.MaxBy(v => v.Version);

    public DocumentVersion? ActiveVersion => _versions.FirstOrDefault(v => v.Version == CurrentVersion);

    public void SetInfo(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Hujjat nomi bo'sh bo'lishi mumkin emas.");

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public DocumentVersion AddVersion(string fileName, string storageKey, string contentHash, long fileSize)
    {
        EnsureNotDeleted();

        var version = new DocumentVersion(Id, NextVersionNumber, fileName, storageKey, contentHash, fileSize);
        _versions.Add(version);
        UpdatedAt = DateTimeOffset.UtcNow;
        return version;
    }

    public void Activate(int version)
    {
        EnsureNotDeleted();

        if (version <= CurrentVersion)
            throw new DomainException($"Versiya {version} joriy versiyadan ({CurrentVersion}) katta bo'lishi kerak.");

        CurrentVersion = version;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        DeletedAt ??= DateTimeOffset.UtcNow;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Hujjat o'chirilgan.");
    }
}
