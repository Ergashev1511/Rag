using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rag.Application.Abstractions;
using Rag.Domain.Enums;
using Rag.Infrastructure.Persistence;

namespace Rag.Infrastructure.VectorStore;

/// <summary>
/// Faqat faol versiya chunk lari qidiriladi (document_version = current_version, deleted_at IS NULL).
/// Hybrid: vector + kalit so'z (tsvector) natijalari RRF bilan birlashtiriladi.
/// </summary>
internal sealed class PgVectorStore(AppDbContext db) : IVectorStore
{
    private const int HybridCandidates = 20;

    private const string ResultColumns = """
        c.document_id, d.title, v.file_name, c.page_number, c.section_title, c.chunk_type, c.content
        """;

    private const string VectorSql = $"""
        SELECT {ResultColumns}, 1 - (c.embedding <=> @embedding::vector) AS score
        FROM document_chunks c
        JOIN documents d ON d.id = c.document_id
        JOIN document_versions v ON v.document_id = d.id AND v.version = d.current_version
        WHERE d.deleted_at IS NULL AND c.document_version = d.current_version
        ORDER BY c.embedding <=> @embedding::vector
        LIMIT @top_k
        """;

    // Kalit so'zlar OR bilan: savoldagi har qanday aniq nom (endpoint, maydon) mos kelsa topiladi.
    private const string HybridSql = $"""
        WITH active AS (
            SELECT c.id, c.embedding, c.search_vector
            FROM document_chunks c
            JOIN documents d ON d.id = c.document_id
            WHERE d.deleted_at IS NULL AND c.document_version = d.current_version
        ),
        vec AS (
            SELECT id,
                   1 - (embedding <=> @embedding::vector) AS similarity,
                   row_number() OVER (ORDER BY embedding <=> @embedding::vector) AS rnk
            FROM active
            ORDER BY embedding <=> @embedding::vector
            LIMIT @candidates
        ),
        kw_query AS (
            SELECT to_tsquery('simple', coalesce(string_agg('''' || replace(lexeme, '''', '''''') || '''', ' | '), '')) AS q
            FROM unnest(tsvector_to_array(to_tsvector('simple', @text))) AS lexeme
            WHERE position(E'\\' IN lexeme) = 0
        ),
        kw AS (
            SELECT a.id, row_number() OVER (ORDER BY ts_rank_cd(a.search_vector, k.q) DESC) AS rnk
            FROM active a, kw_query k
            WHERE a.search_vector @@ k.q
            ORDER BY ts_rank_cd(a.search_vector, k.q) DESC
            LIMIT @candidates
        ),
        fused AS (
            SELECT coalesce(vec.id, kw.id) AS id,
                   coalesce(vec.similarity, 0) AS similarity,
                   kw.id IS NOT NULL AS keyword_hit,
                   coalesce(1.0 / (60 + vec.rnk), 0) + coalesce(1.0 / (60 + kw.rnk), 0) AS rrf
            FROM vec
            FULL OUTER JOIN kw ON vec.id = kw.id
        )
        SELECT {ResultColumns}, f.similarity AS score
        FROM fused f
        JOIN document_chunks c ON c.id = f.id
        JOIN documents d ON d.id = c.document_id
        JOIN document_versions v ON v.document_id = d.id AND v.version = d.current_version
        WHERE f.similarity >= @min_score OR f.keyword_hit
        ORDER BY f.rrf DESC
        LIMIT @top_k
        """;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand(query.Hybrid ? HybridSql : VectorSql, connection);
            command.Parameters.AddWithValue("embedding", ToVectorLiteral(query.Embedding));
            command.Parameters.AddWithValue("top_k", query.TopK);
            if (query.Hybrid)
            {
                command.Parameters.AddWithValue("candidates", HybridCandidates);
                command.Parameters.AddWithValue("text", query.Text);
                command.Parameters.AddWithValue("min_score", query.MinScore);
            }

            var hits = await ReadHitsAsync(command, cancellationToken);
            return query.Hybrid ? hits : hits.Where(h => h.Score >= query.MinScore).ToList();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<List<SearchHit>> ReadHitsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var hits = new List<SearchHit>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hits.Add(new SearchHit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                Enum.Parse<ChunkType>(reader.GetString(5)),
                reader.GetString(6),
                (float)reader.GetDouble(7)));
        }
        return hits;
    }

    private static string ToVectorLiteral(float[] embedding) =>
        "[" + string.Join(',', embedding.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
}
