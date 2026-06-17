using System.ComponentModel.DataAnnotations;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.Entities.Concrete;

namespace MoneyTransferService.Entities.Concrete;

public sealed class Account : Entity
{
    public string AccountNumber { get; set; } = default!;
    public string CurrencyCode { get; set; } = default!;
    public decimal Balance { get; set; }
    public string Status { get; set; } = AccountStatus.ACTIVE;

    // Concurrency token for optimistic concurrency control
    // This property will be automatically updated by the database on each update
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public Guid? IndividualCustomerId { get; set; }
    public IndividualCustomer? IndividualCustomer { get; set; }

    public Guid? CorporateCustomerId { get; set; }
    public CorporateCustomer? CorporateCustomer { get; set; }

    public ICollection<Transfer> OutgoingTransfers { get; set; } = [];
    public ICollection<Transfer> IncomingTransfers { get; set; } = [];
}
