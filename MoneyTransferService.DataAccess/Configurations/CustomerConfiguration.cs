using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyTransferService.Entities.Abstract;

namespace MoneyTransferService.DataAccess.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // inheritance strategy configuration
        builder.UseTpcMappingStrategy();
    }
}
