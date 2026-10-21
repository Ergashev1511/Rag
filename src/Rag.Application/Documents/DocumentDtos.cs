using Rag.Domain.Entities;
using Rag.Domain.Enums;

namespace Rag.Application.Documents;

public sealed record VersionAcceptedDto(Guid DocumentId, int Version, VersionStatus Status);

public sealed record DocumentSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    int CurrentVersion,
    VersionStatus? LatestStatus,
    DateTimeOffset UpdatedAt);

public sealed record DocumentVersionDto(
    int Version,
    string FileName,
    long FileSize,
    int PageCount,
    int ChunkCount,
    VersionStatus Status,
    int Attempts,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IndexedAt)
{
    public static DocumentVersionDto From(DocumentVersion v) => new(
        v.Version, v.FileName, v.FileSize, v.PageCount, v.ChunkCount, v.Status, v.Attempts, v.ErrorMessage, v.CreatedAt, v.IndexedAt);
}

public sealed record DocumentDetailsDto(
    Guid Id,
    string Title,
    string? Description,
    int CurrentVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DocumentVersionDto> Versions);

public sealed record DocumentStatusDto(
    Guid DocumentId,
    int CurrentVersion,
    int LatestVersion,
    VersionStatus Status,
    int Attempts,
    string? ErrorMessage);

public sealed record DownloadDto(Stream Content, string FileName, string ContentType);
