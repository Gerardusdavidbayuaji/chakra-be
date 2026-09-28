using Chakra.Domain.Entities;
using Chakra.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chakra.Infrastructure.Configuration;

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> builder)
    {
        builder.ToTable("Installments");
        
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InstallmentNumber).IsRequired();

        builder.Property(x => x.DueDate).IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(EfConstants.Length.Short)
            .IsRequired();

        builder.Property(x => x.ReminderCount)
            .HasDefaultValue(0);

        builder.Property(x => x.MidtransOrderId)
            .HasMaxLength(EfConstants.Length.Long);

        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(x => x.Premi)
            .WithMany(p => p.Installments)
            .HasForeignKey(x => x.PremiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PremiId);

        builder.HasIndex(x => x.MidtransOrderId)
            .IsUnique()
            .HasFilter("\"MidtransOrderId\" IS NOT NULL");

        builder.HasIndex(x => new { x.PremiId, x.InstallmentNumber })
            .IsUnique();

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}