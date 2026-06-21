using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.Core.Entities.Concrete;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Context;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<Entity>();
        HttpContextAccessor httpContextAccessor = new();

        foreach (var entry in entries)
        {
            if (entry.State is EntityState.Added)
            {
                entry.Property(e => e.CreatedAt).CurrentValue = DateTimeOffset.UtcNow;
            }
            if (entry.State is EntityState.Modified)
            {
                entry.Property(e => e.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
            }
            if (entry.State is EntityState.Deleted)
            {
                throw new InvalidOperationException("Entities cannot be deleted. Please use soft delete instead.");
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    public DbSet<IndividualCustomer> IndividualCustomers { get; set; } = default!;
    public DbSet<CorporateCustomer> CorporateCustomers { get; set; } = default!;
    public DbSet<Account> Accounts { get; set; } = default!;
    public DbSet<Transaction> Transactions { get; set; } = default!;
}