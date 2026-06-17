using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MoneyTransferService.Entities.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Context;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>().UseTpcMappingStrategy();

        modelBuilder.Entity<Account>()
            .HasOne(a => a.IndividualCustomer)
            .WithMany(c => c.Accounts)
            .HasForeignKey(a => a.IndividualCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Account>()
            .HasOne(a => a.CorporateCustomer)
            .WithMany(c => c.Accounts)
            .HasForeignKey(a => a.CorporateCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Account>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_Account_OneOwner",
                "([IndividualCustomerId] IS NOT NULL AND [CorporateCustomerId] IS NULL) OR ([IndividualCustomerId] IS NULL AND [CorporateCustomerId] IS NOT NULL)"
                ));



    }

    public DbSet<IndividualCustomer> IndividualCustomers { get; set; } = default!;
    public DbSet<CorporateCustomer> CorporateCustomers { get; set; } = default!;
    public DbSet<Account> Accounts { get; set; } = default!;
    public DbSet<Transfer> Transfers { get; set; } = default!;
}