/*
    Manual schema update for:
    1. View Hotel Booking History
    2. Update Daily Care Log + persistent realtime notifications

    Run this script after databasenew (1).sql on database PetStoreManagement.
    The script is idempotent and can be executed more than once.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.HotelBookings', N'U') IS NULL
BEGIN
    RAISERROR(N'Thiếu bảng dbo.HotelBookings. Hãy chạy databasenew (1).sql trước.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.PetBioTimelines', N'U') IS NULL
BEGIN
    RAISERROR(N'Thiếu bảng dbo.PetBioTimelines. Hãy chạy databasenew (1).sql trước.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.MedicalRecords', N'U') IS NULL
BEGIN
    RAISERROR(N'Thiếu bảng dbo.MedicalRecords. Hãy chạy databasenew (1).sql trước.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.FoodDiaryLogs', N'U') IS NULL
BEGIN
    RAISERROR(N'Thiếu bảng dbo.FoodDiaryLogs. Hãy chạy databasenew (1).sql trước.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    RAISERROR(N'Thiếu bảng dbo.Customers. Hãy chạy databasenew (1).sql trước.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

    /* ================================================================
       A. VIEW BOOKING HISTORY
       ================================================================ */

    IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckInDate') IS NULL
        ALTER TABLE dbo.HotelBookings ADD ScheduledCheckInDate DATETIME NULL;

    IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckOutDate') IS NULL
        ALTER TABLE dbo.HotelBookings ADD ScheduledCheckOutDate DATETIME NULL;

    IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckInAt') IS NULL
        ALTER TABLE dbo.HotelBookings ADD ActualCheckInAt DATETIME NULL;

    IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckOutAt') IS NULL
        ALTER TABLE dbo.HotelBookings ADD ActualCheckOutAt DATETIME NULL;

    IF COL_LENGTH(N'dbo.PetBioTimelines', N'HotelBookingId') IS NULL
        ALTER TABLE dbo.PetBioTimelines ADD HotelBookingId INT NULL;

    IF COL_LENGTH(N'dbo.MedicalRecords', N'HotelBookingId') IS NULL
        ALTER TABLE dbo.MedicalRecords ADD HotelBookingId INT NULL;

    IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'HotelBookingId') IS NULL
        ALTER TABLE dbo.FoodDiaryLogs ADD HotelBookingId INT NULL;

    IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'OccurredAt') IS NULL
        ALTER TABLE dbo.FoodDiaryLogs ADD OccurredAt DATETIME NULL;

    UPDATE dbo.HotelBookings
    SET ScheduledCheckInDate = COALESCE(ScheduledCheckInDate, CheckInDate),
        ScheduledCheckOutDate = COALESCE(ScheduledCheckOutDate, CheckOutDate),
        ActualCheckInAt = CASE
            WHEN ActualCheckInAt IS NULL
                 AND Status IN (N'Active', N'Đang ở', N'Đã trả')
                THEN CheckInDate
            ELSE ActualCheckInAt
        END,
        ActualCheckOutAt = CASE
            WHEN ActualCheckOutAt IS NULL AND Status = N'Đã trả'
                THEN CheckOutDate
            ELSE ActualCheckOutAt
        END
    WHERE ScheduledCheckInDate IS NULL
       OR ScheduledCheckOutDate IS NULL
       OR (ActualCheckInAt IS NULL AND Status IN (N'Active', N'Đang ở', N'Đã trả'))
       OR (ActualCheckOutAt IS NULL AND Status = N'Đã trả');

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_PetBioTimelines_HotelBookings'
          AND parent_object_id = OBJECT_ID(N'dbo.PetBioTimelines'))
        ALTER TABLE dbo.PetBioTimelines WITH CHECK
            ADD CONSTRAINT FK_PetBioTimelines_HotelBookings
            FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId)
            ON DELETE SET NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_MedicalRecords_HotelBookings'
          AND parent_object_id = OBJECT_ID(N'dbo.MedicalRecords'))
        ALTER TABLE dbo.MedicalRecords WITH CHECK
            ADD CONSTRAINT FK_MedicalRecords_HotelBookings
            FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId)
            ON DELETE SET NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_FoodDiaryLogs_HotelBookings'
          AND parent_object_id = OBJECT_ID(N'dbo.FoodDiaryLogs'))
        ALTER TABLE dbo.FoodDiaryLogs WITH CHECK
            ADD CONSTRAINT FK_FoodDiaryLogs_HotelBookings
            FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId)
            ON DELETE NO ACTION;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_PetBioTimelines_HotelBookingId'
          AND object_id = OBJECT_ID(N'dbo.PetBioTimelines'))
        CREATE INDEX IX_PetBioTimelines_HotelBookingId
            ON dbo.PetBioTimelines(HotelBookingId);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_MedicalRecords_HotelBookingId'
          AND object_id = OBJECT_ID(N'dbo.MedicalRecords'))
        CREATE INDEX IX_MedicalRecords_HotelBookingId
            ON dbo.MedicalRecords(HotelBookingId);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_FoodDiaryLogs_HotelBookingId'
          AND object_id = OBJECT_ID(N'dbo.FoodDiaryLogs'))
        CREATE INDEX IX_FoodDiaryLogs_HotelBookingId
            ON dbo.FoodDiaryLogs(HotelBookingId);

    -- Link legacy timelines only when exactly one booking matches the pet and date.
    UPDATE timeline
    SET HotelBookingId = matched.HotelBookingId
    FROM dbo.PetBioTimelines AS timeline
    CROSS APPLY (
        SELECT MIN(booking.HotelBookingId) AS HotelBookingId,
               COUNT_BIG(*) AS MatchCount
        FROM dbo.HotelBookings AS booking
        WHERE booking.PetId = timeline.PetId
          AND timeline.Date >= DATEADD(HOUR, -12, booking.CheckInDate)
          AND timeline.Date <= DATEADD(HOUR, 12, COALESCE(
              booking.CheckOutDate,
              DATEADD(DAY,
                  CASE WHEN booking.StayDays < 1 THEN 1 ELSE booking.StayDays END,
                  booking.CheckInDate)))
    ) AS matched
    WHERE timeline.HotelBookingId IS NULL
      AND timeline.Type IN (
          N'HotelBookingCreated', N'HotelBookingCancelled', N'HealthCheckIn',
          N'PetCheckIn', N'HotelCageMove', N'HotelCheckOut')
      AND matched.MatchCount = 1;

    -- Link legacy medical records only when exactly one booking matches.
    UPDATE record
    SET HotelBookingId = matched.HotelBookingId
    FROM dbo.MedicalRecords AS record
    CROSS APPLY (
        SELECT MIN(booking.HotelBookingId) AS HotelBookingId,
               COUNT_BIG(*) AS MatchCount
        FROM dbo.HotelBookings AS booking
        WHERE booking.PetId = record.PetId
          AND record.DateCreated >= DATEADD(HOUR, -12, booking.CheckInDate)
          AND record.DateCreated <= DATEADD(HOUR, 12, COALESCE(
              booking.CheckOutDate,
              DATEADD(DAY,
                  CASE WHEN booking.StayDays < 1 THEN 1 ELSE booking.StayDays END,
                  booking.CheckInDate)))
    ) AS matched
    WHERE record.HotelBookingId IS NULL
      AND matched.MatchCount = 1;

    /* ================================================================
       B. DAILY CARE LOG + CUSTOMER WEB NOTIFICATIONS
       ================================================================ */

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
            NotificationId BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_CustomerNotifications PRIMARY KEY,
            CustomerId INT NOT NULL,
            HotelBookingId INT NULL,
            [Type] NVARCHAR(30) NOT NULL
                CONSTRAINT DF_CustomerNotifications_Type DEFAULT N'DailyCare',
            Title NVARCHAR(180) NOT NULL,
            [Message] NVARCHAR(500) NOT NULL,
            LinkUrl NVARCHAR(500) NULL,
            IsRead BIT NOT NULL
                CONSTRAINT DF_CustomerNotifications_IsRead DEFAULT 0,
            CreatedAt DATETIME NOT NULL
                CONSTRAINT DF_CustomerNotifications_CreatedAt DEFAULT GETDATE(),
            ReadAt DATETIME NULL,
            CONSTRAINT FK_CustomerNotifications_Customers
                FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId)
                ON DELETE CASCADE,
            CONSTRAINT FK_CustomerNotifications_HotelBookings
                FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId)
                ON DELETE SET NULL
        );
    END;

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
GO

/* Verification result: every value should be NOT NULL. */
SELECT
    COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckInDate') AS ScheduledCheckInDate,
    COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckInAt') AS ActualCheckInAt,
    COL_LENGTH(N'dbo.PetBioTimelines', N'HotelBookingId') AS TimelineBookingId,
    COL_LENGTH(N'dbo.MedicalRecords', N'HotelBookingId') AS MedicalBookingId,
    COL_LENGTH(N'dbo.FoodDiaryLogs', N'HotelBookingId') AS CareBookingId,
    COL_LENGTH(N'dbo.FoodDiaryLogs', N'ActivityType') AS CareActivityType,
    COL_LENGTH(N'dbo.FoodDiaryLogs', N'MediaUrl') AS CareMediaUrl,
    OBJECT_ID(N'dbo.CustomerNotifications', N'U') AS CustomerNotificationsTable;
GO
