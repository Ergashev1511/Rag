using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Title).IsRequired();
        builder.Property(d => d.CurrentVersion).HasDefaultValue(0);
        builder.Ignore(d => d.IsDeleted);
        builder.Ignore(d => d.NextVersionNumber);
        builder.Ignore(d => d.LatestVersion);
        builder.Ignore(d => d.ActiveVersion);

        builder.HasMany(d => d.Versions)
            .WithOne()
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
