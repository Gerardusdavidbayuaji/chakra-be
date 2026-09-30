using Chakra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chakra.Infrastructure.Configuration;

public class PremiConfiguration : IEntityTypeConfiguration<Premi>
{
    public void Configure(EntityTypeBuilder<Premi> builder)
    {
        builder.ToTable("Premis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
    }
}
