using System.Text;
using MoneyTransferService.Business.Concrete.Infrastructure;
using MoneyTransferService.Business.Options;

namespace MoneyTransferService.Business.Tests.Concrete;

public class TrIBanGeneratorTests
{
    [Fact]
    public void GenerateIban_ShouldReturnTurkishIbanWithExpectedLength()
    {
        // Given
        var options = new IbanOptions
        {
            CountryCode = "TR",
            BankCode = "1",
            ReserveDigit = "0"
        };

        var generator = new TrIBanGenerator(options);
        // When
        var iban = generator.GenerateIban();

        // Then
        Assert.StartsWith("TR", iban);
        Assert.Equal(26, iban.Length); // Turkish IBAN length is 26 characters
    }

    [Fact]
    public void GenerateIban_ShouldReturnValidIban()
    {
        // Given
        var options = new IbanOptions
        {
            CountryCode = "TR",
            BankCode = "1",
            ReserveDigit = "0"
        };

        var generator = new TrIBanGenerator(options);
        // When
        var iban = generator.GenerateIban();

        // Then
        Assert.True(IsValidIban(iban));
    }





    private static bool IsValidIban(string iban)
    {
        // Rearrange the IBAN by moving the first four characters to the end
        var rearranged = $"{iban[4..]}{iban[..4]}";

        var numeric = ConvertLettersToNumbers(rearranged);
        return Mod97(numeric) == 1;
    }

    private static string ConvertLettersToNumbers(string value)
    {
        var result = new StringBuilder();

        foreach (var character in value)
        {
            if (char.IsLetter(character))
                result.Append(char.ToUpperInvariant(character) - 'A' + 10);
            else
                result.Append(character);
        }

        return result.ToString();
    }

    private static int Mod97(string numericValue)
    {
        var remainder = 0;

        foreach (var digit in numericValue)
        {
            remainder = (remainder * 10 + digit - '0') % 97;
        }

        return remainder;
    }
}