using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Entities.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Context;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public DbSet<IndividualCustomer> IndividualCustomers { get; set; } = default!;
    public DbSet<CorporateCustomer> CorporateCustomers { get; set; } = default!;
    public DbSet<Account> Accounts { get; set; } = default!;
    public DbSet<Transfer> Transfers { get; set; } = default!;
}