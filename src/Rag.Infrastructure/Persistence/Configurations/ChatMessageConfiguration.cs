using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.Persistence.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Role).HasConversion<string>().IsRequired();
        builder.Property(m => m.Content).IsRequired();

        builder.HasMany(m => m.Sources)
            .WithOne()
            .HasForeignKey(s => s.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.Sources).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(m => new { m.SessionId, m.CreatedAt });
    }
}
