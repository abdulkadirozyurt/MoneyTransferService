using MoneyTransferService.Business.Requests;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract.Handlers;

public interface IDepositHandler
{
    Task<Transaction> HandleAsync(DepositCommand command, CancellationToken cancellationToken = default);
}
