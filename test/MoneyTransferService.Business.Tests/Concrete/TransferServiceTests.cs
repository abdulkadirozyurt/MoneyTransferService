using Moq;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.Entities.Concrete;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Business.Validators;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.Business.Concrete.Handlers;
using MoneyTransferService.Business.Concrete.BusinessRules;
using MoneyTransferService.Business.Concrete.Infrastructure;
using MoneyTransferService.Business.Abstract.Services;

namespace MoneyTransferService.Business.Tests.Concrete;

public class TransferServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAccountRepository> _accountRepositoryMock;
    private readonly Mock<ITransactionRepository> _transferRepositoryMock;
    private readonly Mock<ITransactionAuditRepository> _auditRepositoryMock;
    private readonly Mock<IAccountLockService> _accountLockServiceMock;
    private readonly TransferHandler _transferHandler;

    private const string SenderIban = "TR000000000000000000000001";
    private const string ReceiverIban = "TR000000000000000000000002";

    public TransferServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _accountRepositoryMock = new Mock<IAccountRepository>();
        _transferRepositoryMock = new Mock<ITransactionRepository>();
        _auditRepositoryMock = new Mock<ITransactionAuditRepository>();
        _accountLockServiceMock = new Mock<IAccountLockService>();

        _accountLockServiceMock.
                Setup(s => s.ExecuteWithAccountLocksAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<Func<Task<Transaction>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyCollection<string>, Func<Task<Transaction>>, CancellationToken>(
                    (_, operation, __) => operation()
                );



        var transactionFactory = new TransactionFactory();
        var retryExecutor = new ConcurrencyRetryExecutor(_auditRepositoryMock.Object);

        _transferHandler = new TransferHandler(
            _unitOfWorkMock.Object,
            _transferRepositoryMock.Object,
            _accountRepositoryMock.Object,
            new TransferCommandValidator(),
            new TransferBusinessRules(),
            _auditRepositoryMock.Object,
            _accountLockServiceMock.Object,
            transactionFactory,
            retryExecutor
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task TransferAsync_ShouldThrowValidationException_WhenAmountIsZeroOrNegative(decimal amount)
    {
        // Arrange & Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                amount,
                "USD",
                "key-123"));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.IsAny<Transaction>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowValidationException_WhenSenderAndReceiverAreSame()
    {
        // Arrange & Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                SenderIban,
                100.00m,
                "USD",
                "key-123"));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldReturnExistingTransfer_WhenIdempotencyKeyAlreadyExists()
    {
        // Arrange
        var existingTransfer = new Transaction
        {
            Id = Guid.NewGuid(),
            SenderIban = SenderIban,
            ReceiverIban = ReceiverIban,
            Amount = 100.00m,
            CurrencyCode = "USD",
            IdempotencyKey = "duplicate-key",
            Status = TransferStatus.COMPLETED
        };

        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTransfer);

        // Act
        var result = await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                100.00m,
                "USD",
                "duplicate-key"));

        // Assert
        result.Should().BeEquivalentTo(existingTransfer);
        _accountRepositoryMock.Verify(r => r.GetByIbanForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepositoryMock.Verify(r => r.Update(It.IsAny<Account>()), Times.Never);
        _transferRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.IsAny<Transaction>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowAccountNotFoundException_WhenSenderAccountDoesNotExist()
    {
        // Arrange
        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(SenderIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                100.00m,
                "USD",
                "key-123"));

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowAccountNotFoundException_WhenReceiverAccountDoesNotExist()
    {
        // Arrange
        var senderAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = SenderIban,
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(SenderIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(ReceiverIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                100.00m,
                "USD",
                "key-123"));

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Theory]
    [InlineData(AccountStatus.FROZEN)]
    [InlineData(AccountStatus.CLOSED)]
    public async Task TransferAsync_ShouldThrowAccountNotActiveException_WhenAccountIsNotActive(string inactiveStatus)
    {
        // Arrange
        var senderAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = SenderIban,
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = inactiveStatus
        };

        var receiverAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = ReceiverIban,
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(SenderIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(ReceiverIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        // Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                100.00m,
                "USD",
                "key-123"));

        // Assert
        await act.Should().ThrowAsync<AccountNotActiveException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowCurrencyMismatchException_WhenCurrenciesDoNotMatch()
    {
        // Arrange
        var senderAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = SenderIban,
            CurrencyCode = "USD", // Account currency mismatched
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = ReceiverIban,
            CurrencyCode = "EUR",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(SenderIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(ReceiverIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        // Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                100.00m,
                "EUR", // Requested transfer currency
                "key-123"));

        // Assert
        await act.Should().ThrowAsync<CurrencyMismatchException>();
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowInsufficientFundsException_AndLogFailure_WhenSenderBalanceIsInsufficient()
    {
        // Arrange
        var amount = 1500.00m; // More than sender's balance
        var senderAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = SenderIban,
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = ReceiverIban,
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(SenderIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(ReceiverIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        // Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                amount,
                "USD",
                "key-123"));

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>();
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.Is<Transaction>(t => t.Amount == amount && t.CurrencyCode == "USD" && t.SenderAccount == senderAccount && t.ReceiverAccount == receiverAccount),
            AuditEventType.FAILED,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_ShouldExecuteTransferSuccessfully_WhenValidationsPass()
    {
        // Arrange
        var amount = 100.00m;
        var description = "Standard Transfer";

        var senderAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = SenderIban,
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = ReceiverIban,
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(SenderIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(ReceiverIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        _transferRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                amount,
                "USD",
                "key-success",
                description));

        // Assert
        senderAccount.Balance.Should().Be(900m);
        receiverAccount.Balance.Should().Be(600m);

        result.Should().NotBeNull();
        result.Amount.Should().Be(amount);
        result.CurrencyCode.Should().Be("USD");
        result.SenderAccountId.Should().Be(senderAccount.Id);
        result.ReceiverAccountId.Should().Be(receiverAccount.Id);
        result.IdempotencyKey.Should().Be("key-success");
        result.Description.Should().Be(description);
        result.Status.Should().Be(TransferStatus.COMPLETED);
        result.TransactionType.Should().Be(TransactionTypes.TRANSFER);

        _accountRepositoryMock.Verify(r => r.Update(senderAccount), Times.Once);
        _accountRepositoryMock.Verify(r => r.Update(receiverAccount), Times.Once);
        _transferRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Initiated called first, then Completed called
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(It.IsAny<Transaction>(), AuditEventType.INITIATED, null), Times.Once);
        _auditRepositoryMock.Verify(a => a.LogTransferAsync(It.IsAny<Transaction>(), AuditEventType.COMPLETED, null), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowConcurrencyException_WhenRowVersionMismatchOccurs()
    {
        // Arrange
        var senderAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = SenderIban,
            CurrencyCode = "USD",
            Balance = 1000m,
            Status = AccountStatus.ACTIVE
        };

        var receiverAccount = new Account
        {
            Id = Guid.NewGuid(),
            Iban = ReceiverIban,
            CurrencyCode = "USD",
            Balance = 500m,
            Status = AccountStatus.ACTIVE
        };

        _transferRepositoryMock
            .Setup(r => r.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(SenderIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderAccount);

        _accountRepositoryMock
            .Setup(r => r.GetByIbanForUpdateAsync(ReceiverIban, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverAccount);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Optimistic concurrency error occurred"));

        // Act
        Func<Task> act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(
                SenderIban,
                ReceiverIban,
                100.00m,
                "USD",
                "key-concurrency"));

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();

        _auditRepositoryMock.Verify(a => a.LogTransferAsync(
            It.Is<Transaction>(t => t.IdempotencyKey == "key-concurrency"),
            AuditEventType.FAILED,
            It.IsAny<string>()), Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
    }
}
