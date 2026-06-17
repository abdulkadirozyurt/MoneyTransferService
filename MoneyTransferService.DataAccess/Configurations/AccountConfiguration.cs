using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder
            .HasOne(a => a.IndividualCustomer)
            .WithMany(c => c.Accounts)
            .HasForeignKey(a => a.IndividualCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(a => a.CorporateCustomer)
            .WithMany(c => c.Accounts)
            .HasForeignKey(a => a.CorporateCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .ToTable(t => t.HasCheckConstraint(
                "CK_Accounts_OneOwner",
                "([IndividualCustomerId] IS NOT NULL AND [CorporateCustomerId] IS NULL) OR ([IndividualCustomerId] IS NULL AND [CorporateCustomerId] IS NOT NULL)"
                ));

        builder
            .Property(a => a.Balance)
            .HasPrecision(18, 2);

        builder
            .Property(a => a.RowVersion)
            .IsRowVersion();

        builder
            .Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20);

    }
}
