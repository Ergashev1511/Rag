using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.Persistence.Configurations;

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.FileName).IsRequired();
        builder.Property(v => v.StorageKey).IsRequired();
        builder.Property(v => v.ContentHash).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().IsRequired();

        builder.HasIndex(v => new { v.DocumentId, v.Version }).IsUnique();
        builder.HasIndex(v => v.Status);
    }
}
