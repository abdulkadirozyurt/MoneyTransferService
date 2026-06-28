using MoneyTransferService.Business.Requests;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract.Handlers;

public interface IWithdrawHandler
{
    Task<Transaction> HandleAsync(WithdrawCommand command, CancellationToken cancellationToken = default);
}
