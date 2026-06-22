SET NOCOUNT ON;

DECLARE @Now datetimeoffset = SYSDATETIMEOFFSET();

DECLARE @SenderCustomerId uniqueidentifier = '11111111-1111-7111-8111-111111111111';
DECLARE @ReceiverCustomerId uniqueidentifier = '22222222-2222-7222-8222-222222222222';
DECLARE @SenderAccountId uniqueidentifier = '33333333-3333-7333-8333-333333333333';
DECLARE @ReceiverAccountId uniqueidentifier = '44444444-4444-7444-8444-444444444444';

IF NOT EXISTS (SELECT 1 FROM IndividualCustomers WHERE Id = @SenderCustomerId)
BEGIN
    INSERT INTO IndividualCustomers (
        Id, CreatedAt, UpdatedAt, DeletedAt, IsDeleted,
        Email, PhoneNumber, FirstName, LastName, NationalIdentityNumber
    )
    VALUES (
        @SenderCustomerId, @Now, NULL, NULL, 0,
        'stress.sender@example.com', '+905551111111', 'Stress', 'Sender', '11111111111'
    );
END;

IF NOT EXISTS (SELECT 1 FROM IndividualCustomers WHERE Id = @ReceiverCustomerId)
BEGIN
    INSERT INTO IndividualCustomers (
        Id, CreatedAt, UpdatedAt, DeletedAt, IsDeleted,
        Email, PhoneNumber, FirstName, LastName, NationalIdentityNumber
    )
    VALUES (
        @ReceiverCustomerId, @Now, NULL, NULL, 0,
        'stress.receiver@example.com', '+905552222222', 'Stress', 'Receiver', '22222222222'
    );
END;

IF NOT EXISTS (SELECT 1 FROM Accounts WHERE Id = @SenderAccountId)
BEGIN
    INSERT INTO Accounts (
        Id, CreatedAt, UpdatedAt, DeletedAt, IsDeleted,
        AccountNumber, CurrencyCode, Balance, Status,
        IndividualCustomerId, CorporateCustomerId
    )
    VALUES (
        @SenderAccountId, @Now, NULL, NULL, 0,
        'TR000000000000000000000001', 'TRY', 100000000.00, 'ACTIVE',
        @SenderCustomerId, NULL
    );
END
ELSE
BEGIN
    UPDATE Accounts
    SET Balance = 100000000.00,
        Status = 'ACTIVE',
        UpdatedAt = @Now,
        IsDeleted = 0,
        DeletedAt = NULL
    WHERE Id = @SenderAccountId;
END;

IF NOT EXISTS (SELECT 1 FROM Accounts WHERE Id = @ReceiverAccountId)
BEGIN
    INSERT INTO Accounts (
        Id, CreatedAt, UpdatedAt, DeletedAt, IsDeleted,
        AccountNumber, CurrencyCode, Balance, Status,
        IndividualCustomerId, CorporateCustomerId
    )
    VALUES (
        @ReceiverAccountId, @Now, NULL, NULL, 0,
        'TR000000000000000000000002', 'TRY', 0.00, 'ACTIVE',
        @ReceiverCustomerId, NULL
    );
END
ELSE
BEGIN
    UPDATE Accounts
    SET Balance = 0.00,
        Status = 'ACTIVE',
        UpdatedAt = @Now,
        IsDeleted = 0,
        DeletedAt = NULL
    WHERE Id = @ReceiverAccountId;
END;

SELECT
    @SenderAccountId AS SenderAccountId,
    @ReceiverAccountId AS ReceiverAccountId;
