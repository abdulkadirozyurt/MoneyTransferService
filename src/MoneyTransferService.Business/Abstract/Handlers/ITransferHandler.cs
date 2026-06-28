using MoneyTransferService.Business.Requests;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Abstract.Handlers;

public interface ITransferHandler
{
    Task<Transaction> HandleAsync(TransferCommand command, CancellationToken cancellationToken = default);
}
