USE MoneyTransferDb;
SET NOCOUNT ON;

DECLARE @Now datetimeoffset = SYSDATETIMEOFFSET();

DECLARE @Fixtures TABLE
(
    ScenarioName nvarchar(50) NOT NULL,
    RoleName nvarchar(50) NOT NULL,
    FixtureIndex int NOT NULL,
    CustomerId uniqueidentifier NOT NULL,
    AccountId uniqueidentifier NOT NULL,
    Iban nvarchar(34) NOT NULL,
    Email nvarchar(254) NOT NULL,
    PhoneNumber nvarchar(20) NOT NULL,
    FirstName nvarchar(100) NOT NULL,
    LastName nvarchar(100) NOT NULL,
    NationalIdentityNumber nvarchar(11) NOT NULL,
    Balance decimal(18, 2) NOT NULL,
    PRIMARY KEY (AccountId)
);

DECLARE @i int;
DECLARE @suffix varchar(12);

-- Scenario 1: overdraft-race. One sender, twenty receivers.
SET @i = 1;
SET @suffix = RIGHT(REPLICATE('0', 12) + CAST(@i AS varchar(12)), 12);
INSERT INTO @Fixtures
VALUES
(
    'overdraft-race',
    'sender',
    @i,
    CONVERT(uniqueidentifier, CONCAT('10000000-0000-7000-8000-', @suffix)),
    CONVERT(uniqueidentifier, CONCAT('20000000-0000-7000-8000-', @suffix)),
    CONCAT('10', RIGHT(REPLICATE('0', 14) + CAST(@i AS varchar(14)), 14)),
    CONCAT('k6-overdraft-sender-', @i, '@example.com'),
    CONCAT('+905900', RIGHT(REPLICATE('0', 6) + CAST(@i AS varchar(6)), 6)),
    'K6Overdraft',
    CONCAT('Sender', @i),
    CONCAT('1', RIGHT(REPLICATE('0', 10) + CAST(@i AS varchar(10)), 10)),
    100.00
);

SET @i = 101;
WHILE @i <= 120
BEGIN
    SET @suffix = RIGHT(REPLICATE('0', 12) + CAST(@i AS varchar(12)), 12);

    INSERT INTO @Fixtures
    VALUES
    (
        'overdraft-race',
        'receiver',
        @i,
        CONVERT(uniqueidentifier, CONCAT('10000000-0000-7000-8000-', @suffix)),
        CONVERT(uniqueidentifier, CONCAT('20000000-0000-7000-8000-', @suffix)),
        CONCAT('10', RIGHT(REPLICATE('0', 14) + CAST(@i AS varchar(14)), 14)),
        CONCAT('k6-overdraft-receiver-', @i, '@example.com'),
        CONCAT('+905900', RIGHT(REPLICATE('0', 6) + CAST(@i AS varchar(6)), 6)),
        'K6Overdraft',
        CONCAT('Receiver', @i),
        CONCAT('1', RIGHT(REPLICATE('0', 10) + CAST(@i AS varchar(10)), 10)),
        0.00
    );

    SET @i += 1;
END;

-- Scenario 2: hotspot-load. One hot receiver, fifty funded senders.
SET @i = 1;
SET @suffix = RIGHT(REPLICATE('0', 12) + CAST(@i AS varchar(12)), 12);
INSERT INTO @Fixtures
VALUES
(
    'hotspot-load',
    'receiver',
    @i,
    CONVERT(uniqueidentifier, CONCAT('11000000-0000-7000-8000-', @suffix)),
    CONVERT(uniqueidentifier, CONCAT('21000000-0000-7000-8000-', @suffix)),
    CONCAT('20', RIGHT(REPLICATE('0', 14) + CAST(@i AS varchar(14)), 14)),
    CONCAT('k6-hotspot-receiver-', @i, '@example.com'),
    CONCAT('+905910', RIGHT(REPLICATE('0', 6) + CAST(@i AS varchar(6)), 6)),
    'K6Hotspot',
    CONCAT('Receiver', @i),
    CONCAT('2', RIGHT(REPLICATE('0', 10) + CAST(@i AS varchar(10)), 10)),
    0.00
);

SET @i = 101;
WHILE @i <= 150
BEGIN
    SET @suffix = RIGHT(REPLICATE('0', 12) + CAST(@i AS varchar(12)), 12);

    INSERT INTO @Fixtures
    VALUES
    (
        'hotspot-load',
        'sender',
        @i,
        CONVERT(uniqueidentifier, CONCAT('11000000-0000-7000-8000-', @suffix)),
        CONVERT(uniqueidentifier, CONCAT('21000000-0000-7000-8000-', @suffix)),
        CONCAT('20', RIGHT(REPLICATE('0', 14) + CAST(@i AS varchar(14)), 14)),
        CONCAT('k6-hotspot-sender-', @i, '@example.com'),
        CONCAT('+905910', RIGHT(REPLICATE('0', 6) + CAST(@i AS varchar(6)), 6)),
        'K6Hotspot',
        CONCAT('Sender', @i),
        CONCAT('2', RIGHT(REPLICATE('0', 10) + CAST(@i AS varchar(10)), 10)),
        100000.00
    );

    SET @i += 1;
END;

-- Scenario 3: baseline-transfer-load. One sender, one receiver.
INSERT INTO @Fixtures
VALUES
(
    'baseline-transfer-load',
    'sender',
    1,
    CONVERT(uniqueidentifier, '33333333-3333-7333-8333-333333333330'),
    CONVERT(uniqueidentifier, '33333333-3333-7333-8333-333333333333'),
    '3000000000000001',
    'k6-baseline-sender-1@example.com',
    '+905930000001',
    'K6Baseline',
    'Sender1',
    '40000000001',
    1000000.00
);

INSERT INTO @Fixtures
VALUES
(
    'baseline-transfer-load',
    'receiver',
    2,
    CONVERT(uniqueidentifier, '44444444-4444-7444-8444-444444444440'),
    CONVERT(uniqueidentifier, '44444444-4444-7444-8444-444444444444'),
    '3000000000000002',
    'k6-baseline-receiver-2@example.com',
    '+905930000002',
    'K6Baseline',
    'Receiver2',
    '40000000002',
    0.00
);

-- Scenario 5: spike-traffic. One hundred funded senders, one hundred receivers.
SET @i = 1;
WHILE @i <= 100
BEGIN
    SET @suffix = RIGHT(REPLICATE('0', 12) + CAST(@i AS varchar(12)), 12);

    INSERT INTO @Fixtures
    VALUES
    (
        'spike-traffic',
        'sender',
        @i,
        CONVERT(uniqueidentifier, CONCAT('12000000-0000-7000-8000-', @suffix)),
        CONVERT(uniqueidentifier, CONCAT('22000000-0000-7000-8000-', @suffix)),
        CONCAT('40', RIGHT(REPLICATE('0', 14) + CAST(@i AS varchar(14)), 14)),
        CONCAT('k6-spike-sender-', @i, '@example.com'),
        CONCAT('+905920', RIGHT(REPLICATE('0', 6) + CAST(@i AS varchar(6)), 6)),
        'K6Spike',
        CONCAT('Sender', @i),
        CONCAT('3', RIGHT(REPLICATE('0', 10) + CAST(@i AS varchar(10)), 10)),
        1000000.00
    );

    SET @i += 1;
END;

SET @i = 101;
WHILE @i <= 200
BEGIN
    SET @suffix = RIGHT(REPLICATE('0', 12) + CAST(@i AS varchar(12)), 12);

    INSERT INTO @Fixtures
    VALUES
    (
        'spike-traffic',
        'receiver',
        @i,
        CONVERT(uniqueidentifier, CONCAT('12000000-0000-7000-8000-', @suffix)),
        CONVERT(uniqueidentifier, CONCAT('22000000-0000-7000-8000-', @suffix)),
        CONCAT('40', RIGHT(REPLICATE('0', 14) + CAST(@i AS varchar(14)), 14)),
        CONCAT('k6-spike-receiver-', @i, '@example.com'),
        CONCAT('+905920', RIGHT(REPLICATE('0', 6) + CAST(@i AS varchar(6)), 6)),
        'K6Spike',
        CONCAT('Receiver', @i),
        CONCAT('3', RIGHT(REPLICATE('0', 10) + CAST(@i AS varchar(10)), 10)),
        0.00
    );

    SET @i += 1;
END;

-- Convert deterministic 16-digit account suffixes into valid Turkish IBANs.
-- TR IBAN: TR + 2 check digits + 5 bank code + 1 reserve digit + 16 account suffix.
DECLARE @IbanAccountId uniqueidentifier;
DECLARE @AccountSuffix nvarchar(16);
DECLARE @Bban nvarchar(22);
DECLARE @NumericIban nvarchar(40);
DECLARE @Remainder int;
DECLARE @Position int;
DECLARE @CheckDigits int;

DECLARE iban_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT AccountId, Iban
FROM @Fixtures;

OPEN iban_cursor;
FETCH NEXT FROM iban_cursor INTO @IbanAccountId, @AccountSuffix;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Bban = CONCAT('00001', '0', @AccountSuffix);
    SET @NumericIban = CONCAT(@Bban, '292700'); -- T=29, R=27, check digits placeholder=00.
    SET @Remainder = 0;
    SET @Position = 1;

    WHILE @Position <= LEN(@NumericIban)
    BEGIN
        SET @Remainder = (@Remainder * 10 + CAST(SUBSTRING(@NumericIban, @Position, 1) AS int)) % 97;
        SET @Position += 1;
    END;

    SET @CheckDigits = 98 - @Remainder;

    UPDATE @Fixtures
    SET Iban = CONCAT('TR', RIGHT(CONCAT('0', CAST(@CheckDigits AS varchar(2))), 2), @Bban)
    WHERE AccountId = @IbanAccountId;

    FETCH NEXT FROM iban_cursor INTO @IbanAccountId, @AccountSuffix;
END;

CLOSE iban_cursor;
DEALLOCATE iban_cursor;

DECLARE @DeletedTransactions int;
DECLARE @InsertedCustomers int;
DECLARE @UpdatedCustomers int;
DECLARE @InsertedAccounts int;
DECLARE @UpdatedAccounts int;

DELETE t
FROM Transactions t
WHERE t.SenderAccountId IN (SELECT AccountId FROM @Fixtures)
   OR t.ReceiverAccountId IN (SELECT AccountId FROM @Fixtures);
SET @DeletedTransactions = @@ROWCOUNT;

INSERT INTO IndividualCustomers
(
    Id,
    CreatedAt,
    UpdatedAt,
    DeletedAt,
    IsDeleted,
    Email,
    PhoneNumber,
    FirstName,
    LastName,
    NationalIdentityNumber
)
SELECT
    f.CustomerId,
    @Now,
    NULL,
    NULL,
    0,
    f.Email,
    f.PhoneNumber,
    f.FirstName,
    f.LastName,
    f.NationalIdentityNumber
FROM @Fixtures f
WHERE NOT EXISTS
(
    SELECT 1
    FROM IndividualCustomers c
    WHERE c.Id = f.CustomerId
);
SET @InsertedCustomers = @@ROWCOUNT;

UPDATE c
SET Email = f.Email,
    PhoneNumber = f.PhoneNumber,
    FirstName = f.FirstName,
    LastName = f.LastName,
    NationalIdentityNumber = f.NationalIdentityNumber,
    UpdatedAt = @Now,
    DeletedAt = NULL,
    IsDeleted = 0
FROM IndividualCustomers c
INNER JOIN @Fixtures f ON f.CustomerId = c.Id;
SET @UpdatedCustomers = @@ROWCOUNT;

INSERT INTO Accounts
(
    Id,
    CreatedAt,
    UpdatedAt,
    DeletedAt,
    IsDeleted,
    Iban,
    CurrencyCode,
    Balance,
    Status,
    IndividualCustomerId,
    CorporateCustomerId
)
SELECT
    f.AccountId,
    @Now,
    NULL,
    NULL,
    0,
    f.Iban,
    'TRY',
    f.Balance,
    'ACTIVE',
    f.CustomerId,
    NULL
FROM @Fixtures f
WHERE NOT EXISTS
(
    SELECT 1
    FROM Accounts a
    WHERE a.Id = f.AccountId
);
SET @InsertedAccounts = @@ROWCOUNT;

UPDATE a
SET Iban = f.Iban,
    CurrencyCode = 'TRY',
    Balance = f.Balance,
    Status = 'ACTIVE',
    IndividualCustomerId = f.CustomerId,
    CorporateCustomerId = NULL,
    UpdatedAt = @Now,
    DeletedAt = NULL,
    IsDeleted = 0
FROM Accounts a
INNER JOIN @Fixtures f ON f.AccountId = a.Id;
SET @UpdatedAccounts = @@ROWCOUNT;

SELECT
    ScenarioName,
    RoleName,
    COUNT(*) AS AccountCount,
    SUM(Balance) AS TotalBalance
FROM @Fixtures
GROUP BY ScenarioName, RoleName
ORDER BY ScenarioName, RoleName;

SELECT
    @DeletedTransactions AS DeletedFixtureTransactions,
    @InsertedCustomers AS InsertedCustomers,
    @UpdatedCustomers AS UpdatedCustomers,
    @InsertedAccounts AS InsertedAccounts,
    @UpdatedAccounts AS UpdatedAccounts;
