using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Configurations;

public class CorporateCustomerConfiguration : IEntityTypeConfiguration<CorporateCustomer>
{
    public void Configure(EntityTypeBuilder<CorporateCustomer> builder)
    {
        builder
            .Property(c => c.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder
            .Property(c => c.TaxNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder
            .HasIndex(c => c.TaxNumber)
            .IsUnique();

        builder
            .Property(c => c.TaxOffice)
            .IsRequired()
            .HasMaxLength(100);
    }
}
