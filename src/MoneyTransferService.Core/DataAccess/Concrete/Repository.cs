using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.Core.Entities.Concrete;
using System.Linq.Expressions;

namespace MoneyTransferService.Core.DataAccess.Concrete;

public class Repository<TEntity, TContext>(TContext context) : IRepository<TEntity>
    where TEntity : Entity
    where TContext : DbContext
{
    private DbSet<TEntity> Entity => context.Set<TEntity>();
    public async Task<IQueryable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> filter = null, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(filter == null)
            ? Entity.AsQueryable()
            : Entity.Where(filter).AsQueryable();
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Entity.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    {
        return await Entity.FirstOrDefaultAsync(filter, cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Entity.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        Entity.Update(entity);
    }

    public void Delete(TEntity entity)
    {
        throw new NotImplementedException();
    }
}
