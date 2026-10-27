using Chakra.Domain.Entities;
using Chakra.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chakra.Infrastructure.Configuration;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(EfConstants.Length.Short)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(OutboxMessage.LastErrorMaxLength);

        // Partial index untuk query antrean worker (hanya baris yang belum terkirim)
        builder.HasIndex(x => x.CreatedAt)
            .HasFilter("\"IsProcessed\" = false");

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}