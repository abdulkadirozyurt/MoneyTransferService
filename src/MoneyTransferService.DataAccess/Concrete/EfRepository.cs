using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Context;

namespace MoneyTransferService.DataAccess.Concrete;

public class EfRepository<T>(ApplicationDbContext context) : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }
}
