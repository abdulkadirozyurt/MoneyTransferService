using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Core.DataAccess.Concrete;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Context;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.DataAccess.Concrete;

public sealed class TransactionRepository(ApplicationDbContext context) : Repository<Transaction, ApplicationDbContext>(context), ITransactionRepository
{
    public async Task<IReadOnlyList<Transaction>> GetHistoryAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await Entities
                            .OrderByDescending(t => t.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);
    }
}
