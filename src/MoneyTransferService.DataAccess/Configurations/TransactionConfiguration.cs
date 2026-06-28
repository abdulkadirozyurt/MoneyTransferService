using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder
            .HasOne(t => t.SenderAccount)
            .WithMany(a => a.OutgoingTransfers)
            .HasForeignKey(t => t.SenderAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(t => t.ReceiverAccount)
            .WithMany(a => a.IncomingTransfers)
            .HasForeignKey(t => t.ReceiverAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .Property(t => t.Amount)
            .HasPrecision(18, 2);

        builder
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        builder.Property(t => t.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder
            .Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder
            .Property(t => t.SenderIban)
            .IsRequired()
            .HasMaxLength(34);

        builder
            .Property(t => t.ReceiverIban)
            .IsRequired()
            .HasMaxLength(34);
    }
}
