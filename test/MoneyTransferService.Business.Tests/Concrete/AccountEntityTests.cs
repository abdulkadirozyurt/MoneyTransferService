using System;
using FluentAssertions;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Entities.Concrete;
using Xunit;

namespace MoneyTransferService.Business.Tests.Concrete;

public class AccountEntityTests
{
    [Fact]
    public void Debit_ShouldDecreaseBalance_WhenAmountIsValidAndFundsSufficient()
    {
        // Arrange
        var account = new Account
        {
            Balance = 1000m,
            Status = AccountStatus.ACTIVE,
            CurrencyCode = "USD",
            AccountNumber = "ACC-123"
        };

        // Act
        account.Debit(400m);

        // Assert
        account.Balance.Should().Be(600m);
    }

    [Fact]
    public void Debit_ShouldThrowArgumentException_WhenAmountIsZeroOrNegative()
    {
        // Arrange
        var account = new Account { Balance = 1000m };

        // Act
        Action actZero = () => account.Debit(0m);
        Action actNeg = () => account.Debit(-50m);

        // Assert
        actZero.Should().Throw<ArgumentException>();
        actNeg.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Debit_ShouldThrowInvalidOperationException_WhenBalanceIsInsufficient()
    {
        // Arrange
        var account = new Account { Balance = 100m };

        // Act
        Action act = () => account.Debit(150m);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Credit_ShouldIncreaseBalance_WhenAmountIsValid()
    {
        // Arrange
        var account = new Account
        {
            Balance = 1000m,
            Status = AccountStatus.ACTIVE,
            CurrencyCode = "USD",
            AccountNumber = "ACC-123"
        };

        // Act
        account.Deposit(300m);

        // Assert
        account.Balance.Should().Be(1300m);
    }

    [Fact]
    public void Credit_ShouldThrowArgumentException_WhenAmountIsZeroOrNegative()
    {
        // Arrange
        var account = new Account { Balance = 1000m };

        // Act
        Action actZero = () => account.Deposit(0m);
        Action actNeg = () => account.Deposit(-50m);

        // Assert
        actZero.Should().Throw<ArgumentException>();
        actNeg.Should().Throw<ArgumentException>();
    }
}
