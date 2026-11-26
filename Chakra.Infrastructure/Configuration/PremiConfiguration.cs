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

        // Koleksi read-only, EF mengisi lewat backing field _installments
        builder.Navigation(x => x.Installments).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ringkasan dihitung dari cicilan, tidak disimpan
        builder.Ignore(x => x.InstallmentsPaid);
        builder.Ignore(x => x.RemainingAmount);
        builder.Ignore(x => x.NextDueDate);

        builder.Property(x => x.RemindersPaused)
            .HasDefaultValue(false);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RemindersPausedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
