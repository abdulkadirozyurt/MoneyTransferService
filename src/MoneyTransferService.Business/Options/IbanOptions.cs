namespace MoneyTransferService.Business.Options;

public sealed class IbanOptions
{
    public string CountryCode { get; set; } = "TR";
    public string BankCode { get; set; } = "00001";
    public string ReserveDigit { get; set; } = "0";
}