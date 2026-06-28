using System.Security.Cryptography;
using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Options;

namespace MoneyTransferService.Business.Concrete;

/// <summary>
/// Generates valid Turkish IBAN values using configurable bank code settings.
/// </summary>
public sealed class TrIBanGenerator(IbanOptions ibanOptions) : IIbanGenerator
{
    private const int AccountSuffixLength = 16;
    private const string TurkeyCountryCode = "TR";

    public string GenerateIban()
    {
        var countryCode = ibanOptions.CountryCode.ToUpperInvariant();
        if (countryCode is not TurkeyCountryCode)
            throw new InvalidOperationException($"Country code '{countryCode}' is not supported. Only '{TurkeyCountryCode}' is supported.");

        // Turkish IBAN uses 5 digits for bank code.
        var bankCode = NormalizeBankCode(ibanOptions.BankCode);

        // Turkish IBAN has one reserved digit after bank code. Usually "0".
        var reserveDigit = NormalizeReserveDigit(ibanOptions.ReserveDigit);

        // Last 16 digits identify the account part inside the bank.
        var accountSuffix = GenerateNumericString(AccountSuffixLength);

        var bban = $"{bankCode}{reserveDigit}{accountSuffix}";

        // Check digits make the final IBAN pass the mod-97 validation rule.
        var checkDigits = CalculateCheckDigits(countryCode, bban);

        return $"{countryCode}{checkDigits}{bban}";
    }

    private string NormalizeBankCode(string bankCode)
    {
        // Bank code must be numeric and exactly 5 digits in Turkish IBAN.
        // Short values are padded from the left so config can use "1" instead of "00001".
        if (string.IsNullOrWhiteSpace(bankCode))
            throw new InvalidOperationException("Bank code cannot be null or empty for IBAN generation.");
        if (bankCode.Length > 5)
            throw new InvalidOperationException($"Bank code '{bankCode}' is too long. It must be 5 digits or less.");
        if (bankCode.Any(c => !char.IsDigit(c)))
            throw new InvalidOperationException($"Bank code '{bankCode}' is invalid. It must contain only digits.");

        return bankCode.PadLeft(5, '0');
    }

    private string NormalizeReserveDigit(string reserveDigit)
    {
        // Reserve digit is part of Turkish IBAN BBAN section.
        // Empty config means default reserve digit "0".
        if (string.IsNullOrWhiteSpace(reserveDigit))
            return "0";

        if (reserveDigit.Length != 1 || !char.IsDigit(reserveDigit[0]))
            throw new InvalidOperationException($"Reserve digit '{reserveDigit}' is invalid. It must be a single digit.");

        return reserveDigit;
    }

    private static string GenerateNumericString(int length)
    {
        // RandomNumberGenerator is used instead of Random because IBAN is financial data.
        var digits = new char[length];

        for (var i = 0; i < length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(digits);
    }

    private static string CalculateCheckDigits(string countryCode, string bban)
    {
        // IBAN check digits are calculated by moving country code + "00" to the end,
        // converting letters to numbers, then applying mod-97.
        var rearranged = $"{bban}{countryCode}00";
        var numeric = ConvertLettersToNumbers(rearranged);
        var remainder = Mod97(numeric);
        var checkDigits = 98 - remainder;

        return checkDigits.ToString("D2");
    }

    private static string ConvertLettersToNumbers(string value)
    {
        // IBAN algorithm converts A=10, B=11, ..., Z=35 before mod-97 calculation.
        var result = new System.Text.StringBuilder();

        foreach (var character in value)
        {
            if (char.IsLetter(character))
            {
                result.Append(char.ToUpperInvariant(character) - 'A' + 10);
            }
            else
            {
                result.Append(character);
            }
        }

        return result.ToString();
    }

    private static int Mod97(string numericValue)
    {
        // The numeric IBAN value can be too large for integer types,
        // so the remainder is calculated digit by digit.
        var remainder = 0;

        foreach (var digit in numericValue)
        {
            remainder = (remainder * 10 + digit - '0') % 97;
        }

        return remainder;
    }
}
