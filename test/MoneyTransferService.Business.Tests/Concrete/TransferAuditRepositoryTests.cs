using FluentAssertions;
using Moq;
using MongoDB.Driver;
using MoneyTransferService.DataAccess.Concrete;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Tests.Concrete;

public class TransferAuditRepositoryTests
{
    private readonly Mock<IMongoDatabase> _databaseMock;
    private readonly Mock<IMongoCollection<TransferAuditLog>> _collectionMock;
    private readonly TransferAuditRepository _transferAuditRepository;

    public TransferAuditRepositoryTests()
    {
        _databaseMock = new Mock<IMongoDatabase>();
        _collectionMock = new Mock<IMongoCollection<TransferAuditLog>>();

        _databaseMock
            .Setup(db => db.GetCollection<TransferAuditLog>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(_collectionMock.Object);

        _transferAuditRepository = new TransferAuditRepository(_databaseMock.Object);
    }

    [Fact]
    public async Task LogTransferAsync_ShouldInsertAuditLog_WithCorrectMappedProperties()
    {
        // Arrange
        var senderAccount = new Account
        {
            AccountNumber = "ACC-12345",
            CurrencyCode = "USD",
            Balance = 1000.00m
        };

        var receiverAccount = new Account
        {
            AccountNumber = "ACC-67890",
            CurrencyCode = "USD",
            Balance = 500.00m
        };

        var transfer = new Transfer
        {
            Amount = 250.00m,
            CurrencyCode = "USD",
            SenderAccount = senderAccount,
            ReceiverAccount = receiverAccount,
            FailureReason = "Original failure reason"
        };
        transfer.Id = Guid.NewGuid();

        string eventType = "Completed";
        string? failureReason = "Specific failure reason";

        TransferAuditLog? capturedLog = null;
        _collectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<TransferAuditLog>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<TransferAuditLog, InsertOneOptions, CancellationToken>((log, opt, token) => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _transferAuditRepository.LogTransferAsync(transfer, eventType, failureReason);

        // Assert
        _collectionMock.Verify(c => c.InsertOneAsync(
            It.IsAny<TransferAuditLog>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        capturedLog.Should().NotBeNull();
        capturedLog!.TransferId.Should().Be(transfer.Id);
        capturedLog.EventType.Should().Be(eventType);
        capturedLog.SenderAccountNumber.Should().Be(senderAccount.AccountNumber);
        capturedLog.ReceiverAccountNumber.Should().Be(receiverAccount.AccountNumber);
        capturedLog.Amount.Should().Be(transfer.Amount);
        capturedLog.CurrencyCode.Should().Be(transfer.CurrencyCode);
        capturedLog.FailureReason.Should().Be(failureReason);
        capturedLog.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogTransferAsync_ShouldUseParameterFailureReason_WhenProvided()
    {
        // Arrange
        var senderAccount = new Account { AccountNumber = "ACC-1" };
        var receiverAccount = new Account { AccountNumber = "ACC-2" };
        var transfer = new Transfer
        {
            SenderAccount = senderAccount,
            ReceiverAccount = receiverAccount,
            FailureReason = "Transfer internal error"
        };
        _collectionMock
            .Setup(c => c.InsertOneAsync(It.IsAny<TransferAuditLog>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _transferAuditRepository.LogTransferAsync(transfer, "Failed", "Parameter level error");

        // Assert
        _collectionMock.Verify(c => c.InsertOneAsync(
            It.Is<TransferAuditLog>(log => log.FailureReason == "Parameter level error"),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogTransferAsync_ShouldFallBackToTransferFailureReason_WhenParameterIsNull()
    {
        // Arrange
        var senderAccount = new Account { AccountNumber = "ACC-1" };
        var receiverAccount = new Account { AccountNumber = "ACC-2" };
        var transfer = new Transfer
        {
            SenderAccount = senderAccount,
            ReceiverAccount = receiverAccount,
            FailureReason = "Transfer internal error"
        };
        _collectionMock
            .Setup(c => c.InsertOneAsync(It.IsAny<TransferAuditLog>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _transferAuditRepository.LogTransferAsync(transfer, "Failed", null);

        // Assert
        _collectionMock.Verify(c => c.InsertOneAsync(
            It.Is<TransferAuditLog>(log => log.FailureReason == "Transfer internal error"),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
