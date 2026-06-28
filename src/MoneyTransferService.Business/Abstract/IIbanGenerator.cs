namespace MoneyTransferService.Business.Abstract;

/// <summary>
/// Generates valid IBAN values for newly created accounts.
/// </summary>
public interface IIbanGenerator
{
    /// <summary>
    /// Generates a new IBAN candidate. Uniqueness must be enforced by the account creation flow.
    /// </summary>
    string GenerateIban();
}
