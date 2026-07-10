SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.SpaServices', N'TargetSpecies') IS NULL
    ALTER TABLE dbo.SpaServices ADD TargetSpecies NVARCHAR(50) NULL;

IF COL_LENGTH(N'dbo.PetBioTimelines', N'HotelBookingId') IS NULL
    ALTER TABLE dbo.PetBioTimelines ADD HotelBookingId INT NULL;

IF COL_LENGTH(N'dbo.MedicalRecords', N'HotelBookingId') IS NULL
    ALTER TABLE dbo.MedicalRecords ADD HotelBookingId INT NULL;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'HotelBookingId') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD HotelBookingId INT NULL;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'OccurredAt') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD OccurredAt DATETIME NULL;

IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckInDate') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ScheduledCheckInDate DATETIME NULL;

IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckOutDate') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ScheduledCheckOutDate DATETIME NULL;

IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckInAt') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ActualCheckInAt DATETIME NULL;

IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckOutAt') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ActualCheckOutAt DATETIME NULL;

EXEC(N'
    UPDATE dbo.HotelBookings
    SET ScheduledCheckInDate = COALESCE(ScheduledCheckInDate, CheckInDate),
        ScheduledCheckOutDate = COALESCE(ScheduledCheckOutDate, CheckOutDate),
        ActualCheckInAt = CASE
            WHEN ActualCheckInAt IS NULL AND Status IN (N''Active'', N''Đang ở'', N''Đã trả'') THEN CheckInDate
            ELSE ActualCheckInAt
        END,
        ActualCheckOutAt = CASE
            WHEN ActualCheckOutAt IS NULL AND Status = N''Đã trả'' THEN CheckOutDate
            ELSE ActualCheckOutAt
        END
    WHERE ScheduledCheckInDate IS NULL
       OR ScheduledCheckOutDate IS NULL
       OR (ActualCheckInAt IS NULL AND Status IN (N''Active'', N''Đang ở'', N''Đã trả''))
       OR (ActualCheckOutAt IS NULL AND Status = N''Đã trả'');
');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PetBioTimelines_HotelBookings')
    ALTER TABLE dbo.PetBioTimelines WITH CHECK ADD CONSTRAINT FK_PetBioTimelines_HotelBookings
        FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MedicalRecords_HotelBookings')
    ALTER TABLE dbo.MedicalRecords WITH CHECK ADD CONSTRAINT FK_MedicalRecords_HotelBookings
        FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FoodDiaryLogs_HotelBookings')
    ALTER TABLE dbo.FoodDiaryLogs WITH CHECK ADD CONSTRAINT FK_FoodDiaryLogs_HotelBookings
        FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PetBioTimelines_HotelBookingId' AND object_id = OBJECT_ID(N'dbo.PetBioTimelines'))
    CREATE INDEX IX_PetBioTimelines_HotelBookingId ON dbo.PetBioTimelines(HotelBookingId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MedicalRecords_HotelBookingId' AND object_id = OBJECT_ID(N'dbo.MedicalRecords'))
    CREATE INDEX IX_MedicalRecords_HotelBookingId ON dbo.MedicalRecords(HotelBookingId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FoodDiaryLogs_HotelBookingId' AND object_id = OBJECT_ID(N'dbo.FoodDiaryLogs'))
    CREATE INDEX IX_FoodDiaryLogs_HotelBookingId ON dbo.FoodDiaryLogs(HotelBookingId);

-- Backfill only when a legacy event matches exactly one stay. Ambiguous rows remain unlinked.
UPDATE timeline
SET HotelBookingId = matches.HotelBookingId
FROM dbo.PetBioTimelines AS timeline
CROSS APPLY (
    SELECT MIN(booking.HotelBookingId) AS HotelBookingId, COUNT_BIG(*) AS MatchCount
    FROM dbo.HotelBookings AS booking
    WHERE booking.PetId = timeline.PetId
      AND timeline.Date >= DATEADD(HOUR, -12, booking.CheckInDate)
      AND timeline.Date <= DATEADD(HOUR, 12, COALESCE(
          booking.CheckOutDate,
          DATEADD(DAY, CASE WHEN booking.StayDays < 1 THEN 1 ELSE booking.StayDays END, booking.CheckInDate)))
) AS matches
WHERE timeline.HotelBookingId IS NULL
  AND timeline.Type IN (N'HotelBookingCreated', N'HotelBookingCancelled', N'HealthCheckIn', N'PetCheckIn', N'HotelCageMove', N'HotelCheckOut')
  AND matches.MatchCount = 1;

UPDATE record
SET HotelBookingId = matches.HotelBookingId
FROM dbo.MedicalRecords AS record
CROSS APPLY (
    SELECT MIN(booking.HotelBookingId) AS HotelBookingId, COUNT_BIG(*) AS MatchCount
    FROM dbo.HotelBookings AS booking
    WHERE booking.PetId = record.PetId
      AND record.DateCreated >= DATEADD(HOUR, -12, booking.CheckInDate)
      AND record.DateCreated <= DATEADD(HOUR, 12, COALESCE(
          booking.CheckOutDate,
          DATEADD(DAY, CASE WHEN booking.StayDays < 1 THEN 1 ELSE booking.StayDays END, booking.CheckInDate)))
) AS matches
WHERE record.HotelBookingId IS NULL
  AND matches.MatchCount = 1;

-- SpaReviews is used by SpaBookingController but is absent from the supplied database script.
IF OBJECT_ID(N'dbo.SpaReviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SpaReviews (
        ReviewId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SpaReviews PRIMARY KEY,
        BookingId INT NOT NULL,
        ServiceId INT NOT NULL,
        GroomerId INT NOT NULL,
        RatingStar INT NOT NULL,
        Comment NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SpaReviews_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_SpaReviews_SpaBookings FOREIGN KEY (BookingId)
            REFERENCES dbo.SpaBookings(BookingId),
        CONSTRAINT CK_SpaReviews_RatingStar CHECK (RatingStar BETWEEN 1 AND 5)
    );
    CREATE UNIQUE INDEX UX_SpaReviews_BookingId ON dbo.SpaReviews(BookingId);
END;

COMMIT TRANSACTION;
