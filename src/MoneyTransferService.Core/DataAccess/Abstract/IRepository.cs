using MoneyTransferService.Core.Entities.Concrete;
using System.Linq.Expressions;

namespace MoneyTransferService.Core.DataAccess.Abstract;

public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<T?> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}
