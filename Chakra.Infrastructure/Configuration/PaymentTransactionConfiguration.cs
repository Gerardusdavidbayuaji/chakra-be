using Chakra.Domain.Entities;
using Chakra.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chakra.Infrastructure.Configuration;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    private const string PendingFilter = "\"Status\" = 'Pending'";

    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.OrderId)
            .HasMaxLength(EfConstants.Length.Short)
            .IsRequired();

        builder.HasIndex(x => x.OrderId).IsUnique();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(EfConstants.Length.Short)
            .IsRequired();

        builder.Property(x => x.RejectionReason)
            .HasConversion<string>()
            .HasMaxLength(EfConstants.Length.Short);

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.ExpiresAt).IsRequired();

        builder.Property(x => x.SnapToken).HasMaxLength(EfConstants.Length.Long);
        builder.Property(x => x.RedirectUrl).HasMaxLength(EfConstants.Length.Long);
        builder.Property(x => x.RefundReference).HasMaxLength(EfConstants.Length.Long);
        builder.Property(x => x.ResolutionNote).HasMaxLength(EfConstants.Length.Long);
        builder.Property(x => x.PaymentType).HasMaxLength(EfConstants.Length.Long);
        builder.Property(x => x.MidtransTransactionId).HasMaxLength(EfConstants.Length.Long);

        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<Installment>()
            .WithMany()
            .HasForeignKey(x => x.InstallmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Premi>()
            .WithMany()
            .HasForeignKey(x => x.PremiId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Installment>()
            .WithMany()
            .HasForeignKey(x => x.AllocatedInstallmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PremiId, x.Status });

        builder.HasIndex(x => x.InstallmentId)
            .IsUnique()
            .HasFilter(PendingFilter);

        builder.HasIndex(x => x.ExpiresAt)
            .HasFilter(PendingFilter);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
