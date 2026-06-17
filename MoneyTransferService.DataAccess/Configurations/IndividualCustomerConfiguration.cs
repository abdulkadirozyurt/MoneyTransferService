using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Configurations;

public class IndividualCustomerConfiguration : IEntityTypeConfiguration<IndividualCustomer>
{
    public void Configure(EntityTypeBuilder<IndividualCustomer> builder)
    {
        builder
            .Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder
            .Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder
            .Property(c => c.NationalIdentityNumber)
            .IsRequired()
            .HasMaxLength(11);

        builder
            .HasIndex(c => c.NationalIdentityNumber)
            .IsUnique();
    }
}
