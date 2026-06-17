using System.ComponentModel.DataAnnotations;
using MoneyTransferService.Core.Entities.Concrete;
using MoneyTransferService.Core.Enums;
using MoneyTransferService.Entities.Abstract;

namespace MoneyTransferService.Entities.Concrete;

public sealed class Account : Entity
{
    public string AccountNumber { get; set; } = default!;
    public string CurrencyCode { get; set; } = default!;
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; }

    public ICollection<Transfer> OutgoingTransfers { get; set; } = [];
    public ICollection<Transfer> IncomingTransfers { get; set; } = [];

}
