using Microsoft.EntityFrameworkCore.Storage;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Context;

namespace MoneyTransferService.DataAccess.Concrete;

public sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _currentTransaction;
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("A transaction is already in progress. Please commit or rollback the current transaction before starting a new one.");

        _currentTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress. Please start a transaction before committing.");

        await _currentTransaction.CommitAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) { return; }

        await _currentTransaction.RollbackAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;

        context.ChangeTracker.Clear(); // Clear the change tracker to discard any pending changes after rollback
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
    }
}