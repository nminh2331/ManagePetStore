SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'HotelBookingId') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD HotelBookingId INT NULL;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'OccurredAt') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD OccurredAt DATETIME NULL;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'ActivityType') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD ActivityType NVARCHAR(30) NOT NULL
        CONSTRAINT DF_FoodDiaryLogs_ActivityType DEFAULT N'General';

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'Title') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD Title NVARCHAR(150) NOT NULL
        CONSTRAINT DF_FoodDiaryLogs_Title DEFAULT N'Nhật ký chăm sóc';

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'MediaUrl') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD MediaUrl NVARCHAR(500) NULL;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'MediaType') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD MediaType NVARCHAR(30) NULL;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'IsVisibleToCustomer') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD IsVisibleToCustomer BIT NOT NULL
        CONSTRAINT DF_FoodDiaryLogs_IsVisibleToCustomer DEFAULT 1;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'CreatedByUserId') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD CreatedByUserId INT NULL;

IF OBJECT_ID(N'dbo.CustomerNotifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerNotifications (
        NotificationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerNotifications PRIMARY KEY,
        CustomerId INT NOT NULL,
        HotelBookingId INT NULL,
        [Type] NVARCHAR(30) NOT NULL CONSTRAINT DF_CustomerNotifications_Type DEFAULT N'DailyCare',
        Title NVARCHAR(180) NOT NULL,
        [Message] NVARCHAR(500) NOT NULL,
        LinkUrl NVARCHAR(500) NULL,
        IsRead BIT NOT NULL CONSTRAINT DF_CustomerNotifications_IsRead DEFAULT 0,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_CustomerNotifications_CreatedAt DEFAULT GETDATE(),
        ReadAt DATETIME NULL,
        CONSTRAINT FK_CustomerNotifications_Customers FOREIGN KEY (CustomerId)
            REFERENCES dbo.Customers(CustomerId) ON DELETE CASCADE,
        CONSTRAINT FK_CustomerNotifications_HotelBookings FOREIGN KEY (HotelBookingId)
            REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FoodDiaryLogs_HotelBookings')
    ALTER TABLE dbo.FoodDiaryLogs WITH CHECK ADD CONSTRAINT FK_FoodDiaryLogs_HotelBookings
        FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE NO ACTION;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FoodDiaryLogs_Booking_OccurredAt'
      AND object_id = OBJECT_ID(N'dbo.FoodDiaryLogs'))
    CREATE INDEX IX_FoodDiaryLogs_Booking_OccurredAt
        ON dbo.FoodDiaryLogs(HotelBookingId, OccurredAt DESC);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomerNotifications_Customer_Unread_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.CustomerNotifications'))
    CREATE INDEX IX_CustomerNotifications_Customer_Unread_CreatedAt
        ON dbo.CustomerNotifications(CustomerId, IsRead, CreatedAt DESC);

COMMIT TRANSACTION;
