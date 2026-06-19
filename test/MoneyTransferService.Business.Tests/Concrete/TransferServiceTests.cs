using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Concrete;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Business.Validators;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;
using Xunit;

namespace MoneyTransferService.Business.Tests.Concrete;

public class TransferServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<Account>> _accountRepositoryMock;
    private readonly Mock<IRepository<Transfer>> _transferRepositoryMock;
    private readonly Mock<ITransferAuditRepository> _auditRepositoryMock;
    private readonly TransferService _transferService;

    public TransferServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _accountRepositoryMock = new Mock<IRepository<Account>>();
        _transferRepositoryMock = new Mock<IRepository<Transfer>>();
        _auditRepositoryMock = new Mock<ITransferAuditRepository>();

        _unitOfWorkMock.Setup(u => u.GetRepository<Account>()).Returns(_accountRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.GetRepository<Transfer>()).Returns(_transferRepositoryMock.Object);

        _transferService = new TransferService(
            _unitOfWorkMock.Object,
            new TransferRequestValidator(),
            new TransferBusinessRules(),
            _auditRepositoryMock.Object);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task TransferAsync_ShouldThrowValidationException_WhenAmountIsZeroOrNegative(decimal amount)
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var currencyCode = "USD";
        var idempotencyKey = "key-123";

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.IsAny<Transfer>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowValidationException_WhenSenderAndReceiverAreSame()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "USD";
        var idempotencyKey = "key-123";

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            accountId,
            accountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldReturnExistingTransfer_WhenIdempotencyKeyAlreadyExists()
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "USD";
        var idempotencyKey = "duplicate-key";

        var existingTransfer = new Transfer
        {
            Id = Guid.NewGuid(),
            SenderAccountId = senderAccountId,
            ReceiverAccountId = receiverAccountId,
            Amount = amount,
            CurrencyCode = currencyCode,
            IdempotencyKey = idempotencyKey,
            Status = TransferStatus.COMPLETED
        };

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { existingTransfer });

        // Act
        var result = await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        result.Should().BeEquivalentTo(existingTransfer);
        // Ensure no storage operations or state updates were made for this request
        _accountRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepositoryMock.Verify(r => r.Update(It.IsAny<Account>()), Times.Never);
        _transferRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Transfer>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.IsAny<Transfer>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowAccountNotFoundException_WhenSenderAccountDoesNotExist()
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "USD";
        var idempotencyKey = "key-123";

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(senderAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowAccountNotFoundException_WhenReceiverAccountDoesNotExist()
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "USD";
        var idempotencyKey = "key-123";

        var senderAccount = new Account
        {
            Id = senderAccountId,
            AccountNumber = "ACC-SENDER",
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(senderAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(receiverAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Theory]
    [InlineData(AccountStatus.FROZEN)]
    [InlineData(AccountStatus.CLOSED)]
    public async Task TransferAsync_ShouldThrowAccountNotActiveException_WhenAccountIsNotActive(string inactiveStatus)
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "USD";
        var idempotencyKey = "key-123";

        var senderAccount = new Account
        {
            Id = senderAccountId,
            AccountNumber = "ACC-SENDER",
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = inactiveStatus
        };

        var receiverAccount = new Account
        {
            Id = receiverAccountId,
            AccountNumber = "ACC-RECEIVER",
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(senderAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(receiverAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<AccountNotActiveException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowCurrencyMismatchException_WhenCurrenciesDoNotMatch()
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "EUR"; // Requested transfer currency
        var idempotencyKey = "key-123";

        var senderAccount = new Account
        {
            Id = senderAccountId,
            AccountNumber = "ACC-SENDER",
            CurrencyCode = "USD", // Account currency mismatched
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = receiverAccountId,
            AccountNumber = "ACC-RECEIVER",
            CurrencyCode = "EUR",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(senderAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(receiverAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<CurrencyMismatchException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowInsufficientFundsException_AndLogFailure_WhenSenderBalanceIsInsufficient()
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 1500.00m; // More than sender's balance
        var currencyCode = "USD";
        var idempotencyKey = "key-123";

        var senderAccount = new Account
        {
            Id = senderAccountId,
            AccountNumber = "ACC-SENDER",
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = receiverAccountId,
            AccountNumber = "ACC-RECEIVER",
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(senderAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(receiverAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>();
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.Is<Transfer>(t => t.Amount == amount && t.CurrencyCode == currencyCode && t.SenderAccount == senderAccount && t.ReceiverAccount == receiverAccount),
            AuditEventType.FAILED,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_ShouldExecuteTransferSuccessfully_WhenValidationsPass()
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "USD";
        var idempotencyKey = "key-success";
        var description = "Standard Transfer";

        var senderAccount = new Account
        {
            Id = senderAccountId,
            AccountNumber = "ACC-SENDER",
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = receiverAccountId,
            AccountNumber = "ACC-RECEIVER",
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(senderAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(receiverAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        _transferRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Transfer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey,
            description);

        // Assert
        senderAccount.Balance.Should().Be(900m);
        receiverAccount.Balance.Should().Be(600m);

        result.Should().NotBeNull();
        result.Amount.Should().Be(amount);
        result.CurrencyCode.Should().Be(currencyCode);
        result.SenderAccountId.Should().Be(senderAccountId);
        result.ReceiverAccountId.Should().Be(receiverAccountId);
        result.IdempotencyKey.Should().Be(idempotencyKey);
        result.Description.Should().Be(description);
        result.Status.Should().Be(TransferStatus.COMPLETED);

        _accountRepositoryMock.Verify(r => r.Update(senderAccount), Times.Once);
        _accountRepositoryMock.Verify(r => r.Update(receiverAccount), Times.Once);
        _transferRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Transfer>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Initiated called first, then Completed called
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(It.IsAny<Transfer>(), AuditEventType.INITIATED, null), Times.Once);
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(It.IsAny<Transfer>(), AuditEventType.COMPLETED, null), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowConcurrencyException_WhenRowVersionMismatchOccurs()
    {
        // Arrange
        var senderAccountId = Guid.NewGuid();
        var receiverAccountId = Guid.NewGuid();
        var amount = 100.00m;
        var currencyCode = "USD";
        var idempotencyKey = "key-concurrency";

        var senderAccount = new Account
        {
            Id = senderAccountId,
            AccountNumber = "ACC-SENDER",
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = receiverAccountId,
            AccountNumber = "ACC-RECEIVER",
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(senderAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIdAsync(receiverAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Optimistic concurrency error occurred"));

        // Act
        Func<Task> act = async () => await _transferService.TransferAsync(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.Is<Transfer>(t => t.IdempotencyKey == idempotencyKey),
            AuditEventType.FAILED,
            It.IsAny<string>()), Times.Once);
    }
}
