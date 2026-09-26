using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Chakra.Infrastructure.Common;
using Chakra.Domain.Entities;

namespace Chakra.Infrastructure.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Name).IsRequired().HasMaxLength(EfConstants.Length.Medium);
        
        builder.Property(x => x.Email).IsRequired().HasMaxLength(EfConstants.Length.Normal);
        builder.HasIndex(x => x.Email).IsUnique();
        
        builder.Property(x => x.SupabaseAuthId).IsRequired(false);
        
        builder.Property(x => x.ChatId).HasMaxLength(EfConstants.Length.Normal);
        
        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}