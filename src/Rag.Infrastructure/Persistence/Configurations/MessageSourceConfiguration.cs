using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.Persistence.Configurations;

internal sealed class MessageSourceConfiguration : IEntityTypeConfiguration<MessageSource>
{
    public void Configure(EntityTypeBuilder<MessageSource> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.FileName).IsRequired();
        builder.Property(s => s.Snippet).IsRequired();
    }
}
