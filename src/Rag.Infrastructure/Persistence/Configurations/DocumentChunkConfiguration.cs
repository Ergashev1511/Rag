using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Pgvector;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.Persistence.Configurations;

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    // bge-m3 o'lchami. Model o'lchami o'zgarsa yangi migratsiya kerak.
    private const int EmbeddingDimensions = 1024;
    public const string SearchVectorColumn = "search_vector";

    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ChunkType).HasConversion<string>().HasDefaultValue(Domain.Enums.ChunkType.Text);
        builder.Property(c => c.Content).IsRequired();
        builder.Property(c => c.EmbeddingModel).IsRequired();

        builder.Property(c => c.Embedding)
            .HasColumnType($"vector({EmbeddingDimensions})")
            .HasConversion(
                v => new Vector(v),
                v => v.ToArray(),
                new ValueComparer<float[]>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Length,
                    v => v.ToArray()))
            .IsRequired();

        // 'simple' — o'zbek tili uchun tayyor konfiguratsiya yo'q, endpoint/maydon nomlarini o'zgartirmaydi.
        builder.Property<NpgsqlTsVector>(SearchVectorColumn)
            .HasColumnName(SearchVectorColumn)
            .HasComputedColumnSql("to_tsvector('simple', coalesce(section_title, '') || ' ' || content)", stored: true);

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.DocumentId, c.DocumentVersion });
        builder.HasIndex(c => c.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        builder.HasIndex(SearchVectorColumn).HasMethod("gin");
    }
}
