using System;
using FluentAssertions;
using MoneyTransferService.Business.Concrete.BusinessRules;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Entities.Concrete;
using Xunit;

namespace MoneyTransferService.Business.Tests.Concrete;

public class TransferBusinessRulesTests
{
    private readonly TransferBusinessRules _businessRules = new();

    [Fact]
    public void EnsureAccountExists_ShouldReturnAccount_WhenAccountExists()
    {
        // Arrange
        var account = CreateAccount();

        // Act
        var result = _businessRules.EnsureAccountExists(account, "Sender", account.Iban);

        // Assert
        result.Should().BeSameAs(account);
    }

    [Fact]
    public void EnsureAccountExists_ShouldThrowAccountNotFoundException_WhenAccountDoesNotExist()
    {
        // Arrange
        var iban = "TR000000000000000000000001";

        // Act
        Action act = () => _businessRules.EnsureAccountExists(null, "Sender", iban);

        // Assert
        act.Should().Throw<AccountNotFoundException>();
    }

    [Fact]
    public void EnsureAccountIsActive_ShouldNotThrow_WhenAccountIsActive()
    {
        // Arrange
        var account = CreateAccount(status: AccountStatus.ACTIVE);

        // Act
        Action act = () => _businessRules.EnsureAccountIsActive(account, "Sender");

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(AccountStatus.FROZEN)]
    [InlineData(AccountStatus.CLOSED)]
    public void EnsureAccountIsActive_ShouldThrowAccountNotActiveException_WhenAccountIsNotActive(string inactiveStatus)
    {
        // Arrange
        var account = CreateAccount(status: inactiveStatus);

        // Act
        Action act = () => _businessRules.EnsureAccountIsActive(account, "Sender");

        // Assert
        act.Should().Throw<AccountNotActiveException>();
    }

    [Fact]
    public void EnsureCurrencyMatches_ShouldNotThrow_WhenCurrencyMatches()
    {
        // Arrange
        var account = CreateAccount(currencyCode: "USD");

        // Act
        Action act = () => _businessRules.EnsureCurrencyMatches(account, "Sender", "USD");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCurrencyMatches_ShouldThrowCurrencyMismatchException_WhenCurrencyDoesNotMatch()
    {
        // Arrange
        var account = CreateAccount(currencyCode: "USD");

        // Act
        Action act = () => _businessRules.EnsureCurrencyMatches(account, "Sender", "EUR");

        // Assert
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(1000, 500)]
    public void EnsureSufficientFunds_ShouldNotThrow_WhenBalanceIsGreaterThanOrEqualToAmount(decimal balance, decimal amount)
    {
        // Arrange
        var account = CreateAccount(balance: balance);

        // Act
        Action act = () => _businessRules.EnsureSufficientFunds(account, amount);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureSufficientFunds_ShouldThrowInsufficientFundsException_WhenBalanceIsLessThanAmount()
    {
        // Arrange
        var account = CreateAccount(balance: 500m);

        // Act
        Action act = () => _businessRules.EnsureSufficientFunds(account, 1000m);

        // Assert
        act.Should().Throw<InsufficientFundsException>();
    }

    private static Account CreateAccount(
        string currencyCode = "USD",
        decimal balance = 1000m,
        string status = AccountStatus.ACTIVE)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Iban = "ACC-TEST",
            CurrencyCode = currencyCode,
            Balance = balance,
            Status = status
        };
    }
}
