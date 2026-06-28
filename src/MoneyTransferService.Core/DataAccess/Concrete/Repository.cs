using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.Core.Entities.Concrete;
using System.Linq.Expressions;

namespace MoneyTransferService.Core.DataAccess.Concrete;

public class Repository<TEntity, TContext>(TContext context) : IRepository<TEntity>
    where TEntity : Entity
    where TContext : DbContext
{
    protected readonly TContext Context = context;
    protected readonly DbSet<TEntity> Entities = context.Set<TEntity>();

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Entities.ToListAsync(cancellationToken);
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Entities.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    {
        return await Entities.FirstOrDefaultAsync(filter, cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        Entities.Update(entity);
    }

    public void Delete(TEntity entity)
    {
        throw new NotImplementedException();
    }
}
