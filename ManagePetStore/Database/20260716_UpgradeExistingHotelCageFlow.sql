-- ==========================================================================================
-- NÂNG CẤP DATABASE CŨ - HOTEL/CAGE FLOW 2026-07-16
-- Database đích: PetStoreManagement
--
-- Mục tiêu:
--   1. Giữ nguyên dữ liệu nghiệp vụ và các phân hệ khác.
--   2. Chuẩn hóa 3 hạng chuồng STANDARD, VIP, LUXURY.
--   3. Chuyển các tham chiếu sang hạng chuồng gần nhất, sau đó xóa danh mục chuồng dư.
--   4. Bổ sung lịch sử vệ sinh, yêu cầu đổi chuồng và lịch sử chuồng theo lần lưu trú.
--   5. Chuyển gói thức ăn OwnerProvided cũ sang sản phẩm Hotel Default nếu có đủ schema.
--
-- Có thể chạy lại script. Nên backup database trước khi chạy trên môi trường dùng chung.
-- ==========================================================================================

USE [PetStoreManagement];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.RoomTypes', N'U') IS NULL
BEGIN
    THROW 51100, N'Thiếu bảng dbo.RoomTypes. Database không đúng schema PetStoreManagement.', 1;
END;
IF OBJECT_ID(N'dbo.Cages', N'U') IS NULL
BEGIN
    THROW 51101, N'Thiếu bảng dbo.Cages. Database không đúng schema PetStoreManagement.', 1;
END;
IF OBJECT_ID(N'dbo.HotelBookings', N'U') IS NULL
BEGIN
    THROW 51102, N'Thiếu bảng dbo.HotelBookings. Database không đúng schema PetStoreManagement.', 1;
END;
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    THROW 51103, N'Thiếu bảng Customers/Users cần cho luồng Hotel.', 1;
END;
GO

-- Tách batch để SQL Server nhận biết cột Code trước các lệnh UPDATE phía sau.
IF COL_LENGTH(N'dbo.RoomTypes', N'Code') IS NULL
    ALTER TABLE dbo.RoomTypes ADD Code NVARCHAR(20) NULL;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    -- Giữ lại dữ liệu loại chuồng cũ bằng mã LEGACY; không xóa bản ghi.
    UPDATE dbo.RoomTypes
    SET Code = N'LEGACY_' + CONVERT(NVARCHAR(11), RoomTypeId),
        Status = 0
    WHERE Code IS NULL
       OR LTRIM(RTRIM(Code)) = N''
       OR UPPER(LTRIM(RTRIM(Code))) NOT IN (N'STANDARD', N'VIP', N'LUXURY');

    UPDATE dbo.RoomTypes
    SET Code = UPPER(LTRIM(RTRIM(Code)))
    WHERE UPPER(LTRIM(RTRIM(Code))) IN (N'STANDARD', N'VIP', N'LUXURY');

    -- Nếu database cũ từng có mã canonical bị trùng, giữ bản ghi có ID nhỏ nhất.
    ;WITH DuplicateCanonical AS
    (
        SELECT RoomTypeId,
               ROW_NUMBER() OVER (PARTITION BY Code ORDER BY RoomTypeId) AS DuplicateOrder
        FROM dbo.RoomTypes
        WHERE Code IN (N'STANDARD', N'VIP', N'LUXURY')
    )
    UPDATE roomType
    SET Code = N'LEGACY_' + CONVERT(NVARCHAR(11), roomType.RoomTypeId),
        Status = 0
    FROM dbo.RoomTypes AS roomType
    INNER JOIN DuplicateCanonical AS duplicate
        ON duplicate.RoomTypeId = roomType.RoomTypeId
    WHERE duplicate.DuplicateOrder > 1;

    DECLARE @StandardRoomTypeId INT =
    (
        SELECT TOP (1) RoomTypeId
        FROM dbo.RoomTypes
        WHERE Code = N'STANDARD'
        ORDER BY RoomTypeId
    );
    IF @StandardRoomTypeId IS NULL
        SELECT TOP (1) @StandardRoomTypeId = RoomTypeId
        FROM dbo.RoomTypes
        WHERE Type LIKE N'%Standard%'
           OR Type LIKE N'%Tiêu chuẩn%'
        ORDER BY CASE WHEN RoomTypeId = 2 THEN 0 ELSE 1 END, RoomTypeId;

    DECLARE @VipRoomTypeId INT =
    (
        SELECT TOP (1) RoomTypeId
        FROM dbo.RoomTypes
        WHERE Code = N'VIP'
        ORDER BY RoomTypeId
    );
    IF @VipRoomTypeId IS NULL
        SELECT TOP (1) @VipRoomTypeId = RoomTypeId
        FROM dbo.RoomTypes
        WHERE RoomTypeId <> ISNULL(@StandardRoomTypeId, -1)
          AND Type LIKE N'%VIP%'
        ORDER BY CASE WHEN RoomTypeId = 1 THEN 0 ELSE 1 END, RoomTypeId;

    DECLARE @LuxuryRoomTypeId INT =
    (
        SELECT TOP (1) RoomTypeId
        FROM dbo.RoomTypes
        WHERE Code = N'LUXURY'
        ORDER BY RoomTypeId
    );
    IF @LuxuryRoomTypeId IS NULL
        SELECT TOP (1) @LuxuryRoomTypeId = RoomTypeId
        FROM dbo.RoomTypes
        WHERE RoomTypeId NOT IN (ISNULL(@StandardRoomTypeId, -1), ISNULL(@VipRoomTypeId, -1))
          AND (Type LIKE N'%Luxury%' OR Type LIKE N'%Penthouse%')
        ORDER BY CASE WHEN RoomTypeId = 3 THEN 0 ELSE 1 END, RoomTypeId;

    -- Database cũ thường có ID 1/2/3. Chỉ dùng fallback ID khi chưa tìm thấy theo tên/mã.
    IF @StandardRoomTypeId IS NULL
        SELECT @StandardRoomTypeId = RoomTypeId
        FROM dbo.RoomTypes
        WHERE RoomTypeId = 2 AND Code LIKE N'LEGACY[_]%';
    IF @VipRoomTypeId IS NULL
        SELECT @VipRoomTypeId = RoomTypeId
        FROM dbo.RoomTypes
        WHERE RoomTypeId = 1
          AND RoomTypeId <> ISNULL(@StandardRoomTypeId, -1)
          AND Code LIKE N'LEGACY[_]%';
    IF @LuxuryRoomTypeId IS NULL
        SELECT @LuxuryRoomTypeId = RoomTypeId
        FROM dbo.RoomTypes
        WHERE RoomTypeId = 3
          AND RoomTypeId NOT IN (ISNULL(@StandardRoomTypeId, -1), ISNULL(@VipRoomTypeId, -1))
          AND Code LIKE N'LEGACY[_]%';

    IF @StandardRoomTypeId IS NULL
    BEGIN
        INSERT dbo.RoomTypes
            (Code, Type, Size, Capacity, HourlyPrice, DailyPrice, HasAC, HasCamera, HasPremiumFood, Status)
        VALUES
            (N'STANDARD', N'Phòng Standard', N'1.0m x 1.0m', 1, 25000, 200000, 1, 0, 0, 1);
        SET @StandardRoomTypeId = CONVERT(INT, SCOPE_IDENTITY());
    END;

    IF @VipRoomTypeId IS NULL
    BEGIN
        INSERT dbo.RoomTypes
            (Code, Type, Size, Capacity, HourlyPrice, DailyPrice, HasAC, HasCamera, HasPremiumFood, Status)
        VALUES
            (N'VIP', N'Phòng VIP', N'1.5m x 1.5m', 1, 50000, 500000, 1, 1, 0, 1);
        SET @VipRoomTypeId = CONVERT(INT, SCOPE_IDENTITY());
    END;

    IF @LuxuryRoomTypeId IS NULL
    BEGIN
        INSERT dbo.RoomTypes
            (Code, Type, Size, Capacity, HourlyPrice, DailyPrice, HasAC, HasCamera, HasPremiumFood, Status)
        VALUES
            (N'LUXURY', N'Phòng Luxury', N'2.0m x 1.8m', 1, 80000, 750000, 1, 1, 0, 1);
        SET @LuxuryRoomTypeId = CONVERT(INT, SCOPE_IDENTITY());
    END;

    UPDATE dbo.RoomTypes
    SET Code = N'STANDARD', Type = N'Phòng Standard', Size = N'1.0m x 1.0m', Capacity = 1,
        HourlyPrice = 25000, DailyPrice = 200000, HasAC = 1, HasCamera = 0, HasPremiumFood = 0, Status = 1
    WHERE RoomTypeId = @StandardRoomTypeId;

    UPDATE dbo.RoomTypes
    SET Code = N'VIP', Type = N'Phòng VIP', Size = N'1.5m x 1.5m', Capacity = 1,
        HourlyPrice = 50000, DailyPrice = 500000, HasAC = 1, HasCamera = 1, HasPremiumFood = 0, Status = 1
    WHERE RoomTypeId = @VipRoomTypeId;

    UPDATE dbo.RoomTypes
    SET Code = N'LUXURY', Type = N'Phòng Luxury', Size = N'2.0m x 1.8m', Capacity = 1,
        HourlyPrice = 80000, DailyPrice = 750000, HasAC = 1, HasCamera = 1, HasPremiumFood = 0, Status = 1
    WHERE RoomTypeId = @LuxuryRoomTypeId;

    UPDATE dbo.RoomTypes
    SET Status = 0
    WHERE RoomTypeId NOT IN (@StandardRoomTypeId, @VipRoomTypeId, @LuxuryRoomTypeId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

ALTER TABLE dbo.RoomTypes ALTER COLUMN Code NVARCHAR(20) NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.RoomTypes')
      AND name = N'UX_RoomTypes_Code'
)
    CREATE UNIQUE INDEX UX_RoomTypes_Code ON dbo.RoomTypes(Code);
GO

-- Thời gian kế hoạch/thực tế được code Hotel hiện tại sử dụng.
IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckInDate') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ScheduledCheckInDate DATETIME NULL;
IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckOutDate') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ScheduledCheckOutDate DATETIME NULL;
IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckInAt') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ActualCheckInAt DATETIME NULL;
IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckOutAt') IS NULL
    ALTER TABLE dbo.HotelBookings ADD ActualCheckOutAt DATETIME NULL;
GO

UPDATE dbo.HotelBookings
SET ScheduledCheckInDate = COALESCE(ScheduledCheckInDate, CheckInDate),
    ScheduledCheckOutDate = COALESCE(ScheduledCheckOutDate, CheckOutDate),
    ActualCheckInAt = CASE
        WHEN ActualCheckInAt IS NULL AND Status IN (N'Active', N'Đang ở', N'Đã trả') THEN CheckInDate
        ELSE ActualCheckInAt
    END,
    ActualCheckOutAt = CASE
        WHEN ActualCheckOutAt IS NULL AND Status = N'Đã trả' THEN CheckOutDate
        ELSE ActualCheckOutAt
    END;
GO

IF OBJECT_ID(N'dbo.RoomMaintenanceLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomMaintenanceLogs
    (
        MaintenanceLogId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_RoomMaintenanceLogs PRIMARY KEY,
        CageId NVARCHAR(20) NOT NULL,
        PreviousStatus NVARCHAR(30) NULL,
        NewStatus NVARCHAR(30) NOT NULL,
        Reason NVARCHAR(500) NOT NULL,
        StartedAt DATETIME NOT NULL
            CONSTRAINT DF_RoomMaintenanceLogs_StartedAt DEFAULT GETDATE(),
        EndedAt DATETIME NULL,
        CreatedByUserId INT NULL,
        EndedByUserId INT NULL,
        CreatedByName NVARCHAR(100) NULL,
        EndedByName NVARCHAR(100) NULL,
        Note NVARCHAR(1000) NULL,
        CONSTRAINT FK_RoomMaintenanceLogs_Cages
            FOREIGN KEY (CageId) REFERENCES dbo.Cages(CageId) ON DELETE CASCADE,
        CONSTRAINT FK_RoomMaintenanceLogs_CreatedBy
            FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_RoomMaintenanceLogs_EndedBy
            FOREIGN KEY (EndedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_RoomMaintenanceLogs_Time
            CHECK (EndedAt IS NULL OR EndedAt >= StartedAt),
        CONSTRAINT CK_RoomMaintenanceLogs_NewStatus
            CHECK (NewStatus IN (N'Đang dọn dẹp', N'Bảo trì', N'Khóa'))
    );

    CREATE INDEX IX_RoomMaintenanceLogs_CageId
        ON dbo.RoomMaintenanceLogs(CageId);
    CREATE INDEX IX_RoomMaintenanceLogs_StartedAt
        ON dbo.RoomMaintenanceLogs(StartedAt);
    CREATE UNIQUE INDEX UX_RoomMaintenanceLogs_OneOpenPerCage
        ON dbo.RoomMaintenanceLogs(CageId)
        WHERE EndedAt IS NULL;
END;
GO

IF OBJECT_ID(N'dbo.HotelCageChangeRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelCageChangeRequests
    (
        ChangeRequestId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HotelCageChangeRequests PRIMARY KEY,
        HotelBookingId INT NOT NULL,
        CustomerId INT NOT NULL,
        SourceCageId NVARCHAR(20) NOT NULL,
        TargetCageId NVARCHAR(20) NOT NULL,
        Reason NVARCHAR(500) NOT NULL,
        Status NVARCHAR(20) NOT NULL
            CONSTRAINT DF_HotelCageChangeRequests_Status DEFAULT N'Pending',
        RemainingDaysSnapshot INT NOT NULL,
        SourceDailyPriceSnapshot DECIMAL(18,2) NOT NULL,
        TargetDailyPriceSnapshot DECIMAL(18,2) NOT NULL,
        PriceDifferenceSnapshot DECIMAL(18,2) NOT NULL,
        RequestedAt DATETIME NOT NULL
            CONSTRAINT DF_HotelCageChangeRequests_RequestedAt DEFAULT GETDATE(),
        ProcessedAt DATETIME NULL,
        ProcessedByUserId INT NULL,
        ProcessedByName NVARCHAR(100) NULL,
        DecisionNote NVARCHAR(1000) NULL,
        AppliedAt DATETIME NULL,
        CONSTRAINT FK_HotelCageChangeRequests_HotelBookings
            FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE CASCADE,
        CONSTRAINT FK_HotelCageChangeRequests_Customers
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
        CONSTRAINT FK_HotelCageChangeRequests_SourceCage
            FOREIGN KEY (SourceCageId) REFERENCES dbo.Cages(CageId),
        CONSTRAINT FK_HotelCageChangeRequests_TargetCage
            FOREIGN KEY (TargetCageId) REFERENCES dbo.Cages(CageId),
        CONSTRAINT FK_HotelCageChangeRequests_ProcessedBy
            FOREIGN KEY (ProcessedByUserId) REFERENCES dbo.Users(UserId) ON DELETE SET NULL,
        CONSTRAINT CK_HotelCageChangeRequests_Status
            CHECK (Status IN (N'Pending', N'Approved', N'Rejected')),
        CONSTRAINT CK_HotelCageChangeRequests_Cages
            CHECK (SourceCageId <> TargetCageId),
        CONSTRAINT CK_HotelCageChangeRequests_Prices
            CHECK (RemainingDaysSnapshot > 0
                   AND SourceDailyPriceSnapshot >= 0
                   AND TargetDailyPriceSnapshot >= 0)
    );

    CREATE INDEX IX_HotelCageChangeRequests_Booking_Status
        ON dbo.HotelCageChangeRequests(HotelBookingId, Status);
    CREATE INDEX IX_HotelCageChangeRequests_RequestedAt
        ON dbo.HotelCageChangeRequests(RequestedAt);
    CREATE UNIQUE INDEX UX_HotelCageChangeRequests_OnePendingPerBooking
        ON dbo.HotelCageChangeRequests(HotelBookingId)
        WHERE Status = N'Pending';
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HotelCageChangeRequests') AND name = N'IX_HotelCageChangeRequests_Booking_Status')
    CREATE INDEX IX_HotelCageChangeRequests_Booking_Status
        ON dbo.HotelCageChangeRequests(HotelBookingId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HotelCageChangeRequests') AND name = N'IX_HotelCageChangeRequests_RequestedAt')
    CREATE INDEX IX_HotelCageChangeRequests_RequestedAt
        ON dbo.HotelCageChangeRequests(RequestedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HotelCageChangeRequests') AND name = N'UX_HotelCageChangeRequests_OnePendingPerBooking')
    CREATE UNIQUE INDEX UX_HotelCageChangeRequests_OnePendingPerBooking
        ON dbo.HotelCageChangeRequests(HotelBookingId)
        WHERE Status = N'Pending';
GO

IF OBJECT_ID(N'dbo.HotelCageStaySegments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelCageStaySegments
    (
        StaySegmentId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HotelCageStaySegments PRIMARY KEY,
        HotelBookingId INT NOT NULL,
        CageId NVARCHAR(20) NOT NULL,
        RoomTypeId INT NOT NULL,
        DailyPriceSnapshot DECIMAL(18,2) NOT NULL,
        StartedAt DATETIME NOT NULL,
        EndedAt DATETIME NULL,
        StartReason NVARCHAR(30) NOT NULL
            CONSTRAINT DF_HotelCageStaySegments_StartReason DEFAULT N'CheckIn',
        EndReason NVARCHAR(100) NULL,
        CreatedAt DATETIME NOT NULL
            CONSTRAINT DF_HotelCageStaySegments_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_HotelCageStaySegments_HotelBookings
            FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE CASCADE,
        CONSTRAINT FK_HotelCageStaySegments_Cages
            FOREIGN KEY (CageId) REFERENCES dbo.Cages(CageId),
        CONSTRAINT FK_HotelCageStaySegments_RoomTypes
            FOREIGN KEY (RoomTypeId) REFERENCES dbo.RoomTypes(RoomTypeId),
        CONSTRAINT CK_HotelCageStaySegments_Time
            CHECK (EndedAt IS NULL OR EndedAt >= StartedAt),
        CONSTRAINT CK_HotelCageStaySegments_Price
            CHECK (DailyPriceSnapshot >= 0)
    );

    CREATE INDEX IX_HotelCageStaySegments_Booking_StartedAt
        ON dbo.HotelCageStaySegments(HotelBookingId, StartedAt);
    CREATE UNIQUE INDEX UX_HotelCageStaySegments_OneOpenPerBooking
        ON dbo.HotelCageStaySegments(HotelBookingId)
        WHERE EndedAt IS NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HotelCageStaySegments') AND name = N'IX_HotelCageStaySegments_Booking_StartedAt')
    CREATE INDEX IX_HotelCageStaySegments_Booking_StartedAt
        ON dbo.HotelCageStaySegments(HotelBookingId, StartedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HotelCageStaySegments') AND name = N'UX_HotelCageStaySegments_OneOpenPerBooking')
    CREATE UNIQUE INDEX UX_HotelCageStaySegments_OneOpenPerBooking
        ON dbo.HotelCageStaySegments(HotelBookingId)
        WHERE EndedAt IS NULL;
GO

-- Sau khi các bảng tham chiếu đã sẵn sàng, gom toàn bộ loại chuồng cũ về 3 hạng chuẩn.
-- Mapping dựa trên mức giá gần nhất để giữ ý nghĩa kinh doanh của chuồng cũ.
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @StandardIdForCleanup INT =
        (SELECT TOP (1) RoomTypeId FROM dbo.RoomTypes WHERE Code = N'STANDARD');
    DECLARE @VipIdForCleanup INT =
        (SELECT TOP (1) RoomTypeId FROM dbo.RoomTypes WHERE Code = N'VIP');
    DECLARE @LuxuryIdForCleanup INT =
        (SELECT TOP (1) RoomTypeId FROM dbo.RoomTypes WHERE Code = N'LUXURY');

    IF @StandardIdForCleanup IS NULL OR @VipIdForCleanup IS NULL OR @LuxuryIdForCleanup IS NULL
        THROW 51104, N'Không thể xác định đủ ba hạng chuồng chuẩn để dọn dữ liệu cũ.', 1;

    DECLARE @LegacyRoomTypeMapping TABLE
    (
        LegacyRoomTypeId INT NOT NULL PRIMARY KEY,
        TargetRoomTypeId INT NOT NULL
    );

    INSERT @LegacyRoomTypeMapping (LegacyRoomTypeId, TargetRoomTypeId)
    SELECT legacy.RoomTypeId, nearestRoomType.RoomTypeId
    FROM dbo.RoomTypes AS legacy
    CROSS APPLY
    (
        SELECT TOP (1) supported.RoomTypeId
        FROM dbo.RoomTypes AS supported
        WHERE supported.RoomTypeId IN (@StandardIdForCleanup, @VipIdForCleanup, @LuxuryIdForCleanup)
        ORDER BY ABS(supported.DailyPrice - legacy.DailyPrice), supported.RoomTypeId
    ) AS nearestRoomType
    WHERE legacy.RoomTypeId NOT IN (@StandardIdForCleanup, @VipIdForCleanup, @LuxuryIdForCleanup);

    UPDATE cage
    SET cage.RoomTypeId = mapping.TargetRoomTypeId
    FROM dbo.Cages AS cage
    INNER JOIN @LegacyRoomTypeMapping AS mapping
        ON mapping.LegacyRoomTypeId = cage.RoomTypeId;

    IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL
    BEGIN
        UPDATE orderItem
        SET orderItem.RoomTypeId = mapping.TargetRoomTypeId
        FROM dbo.OrderItems AS orderItem
        INNER JOIN @LegacyRoomTypeMapping AS mapping
            ON mapping.LegacyRoomTypeId = orderItem.RoomTypeId;
    END;

    UPDATE segment
    SET segment.RoomTypeId = mapping.TargetRoomTypeId
    FROM dbo.HotelCageStaySegments AS segment
    INNER JOIN @LegacyRoomTypeMapping AS mapping
        ON mapping.LegacyRoomTypeId = segment.RoomTypeId;

    DELETE roomType
    FROM dbo.RoomTypes AS roomType
    INNER JOIN @LegacyRoomTypeMapping AS mapping
        ON mapping.LegacyRoomTypeId = roomType.RoomTypeId;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.RoomTypes')
      AND name = N'CK_RoomTypes_Code'
)
    ALTER TABLE dbo.RoomTypes WITH CHECK
        ADD CONSTRAINT CK_RoomTypes_Code
        CHECK (Code IN (N'STANDARD', N'VIP', N'LUXURY'));
GO

BEGIN TRY
    BEGIN TRANSACTION;

    -- Backfill một chặng chuồng cho booking cũ đang ở/đã trả; không tạo cho booking đã hủy/chưa check-in.
    INSERT dbo.HotelCageStaySegments
        (HotelBookingId, CageId, RoomTypeId, DailyPriceSnapshot,
         StartedAt, EndedAt, StartReason, EndReason, CreatedAt)
    SELECT booking.HotelBookingId,
           booking.CageId,
           cage.RoomTypeId,
           booking.BaseDailyPrice,
           dates.StartedAt,
           CASE
               WHEN booking.Status = N'Đã trả'
                   THEN CASE WHEN dates.CandidateEndAt >= dates.StartedAt
                             THEN dates.CandidateEndAt ELSE dates.StartedAt END
               ELSE NULL
           END,
           N'Migration',
           CASE WHEN booking.Status = N'Đã trả' THEN N'CheckOutBeforeMigration' ELSE NULL END,
           GETDATE()
    FROM dbo.HotelBookings AS booking
    INNER JOIN dbo.Cages AS cage ON cage.CageId = booking.CageId
    CROSS APPLY
    (
        SELECT COALESCE(booking.ActualCheckInAt, booking.ScheduledCheckInDate, booking.CheckInDate) AS StartedAt,
               COALESCE(booking.ActualCheckOutAt, booking.ScheduledCheckOutDate,
                        booking.CheckOutDate, booking.CheckInDate) AS CandidateEndAt
    ) AS dates
    WHERE booking.Status IN (N'Active', N'Đang ở', N'Đã trả')
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.HotelCageStaySegments AS segment
          WHERE segment.HotelBookingId = booking.HotelBookingId
      );

    UPDATE cage
    SET cage.Status = N'Đang dùng'
    FROM dbo.Cages AS cage
    WHERE cage.Status IN (N'Trống', N'Đang ở', N'Đang dùng')
      AND EXISTS
      (
          SELECT 1
          FROM dbo.HotelBookings AS booking
          WHERE booking.CageId = cage.CageId
            AND booking.Status IN (N'Active', N'Đang ở')
      );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Chuyển dữ liệu mang thức ăn ngoài cũ nếu database đã có đủ migration sản phẩm Hotel.
IF OBJECT_ID(N'dbo.HotelBookingFoodPlans', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'ProductSku') IS NOT NULL
   AND COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'ProductUnitSnapshot') IS NOT NULL
   AND COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'BasePricePerDaySnapshot') IS NOT NULL
   AND COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'PortionMultiplierSnapshot') IS NOT NULL
   AND COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'InventoryQuantityDeducted') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'HOTEL-FOOD-DEFAULT')
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE foodPlan
        SET foodPlan.ProductSku = N''HOTEL-FOOD-DEFAULT'',
            foodPlan.PlanType = N''HotelProduct'',
            foodPlan.FoodNameSnapshot = product.Name,
            foodPlan.ProductUnitSnapshot = product.Unit,
            foodPlan.BasePricePerDaySnapshot = product.Price,
            foodPlan.PricePerDaySnapshot = product.Price,
            foodPlan.PortionMultiplierSnapshot = CASE
                WHEN foodPlan.PortionMultiplierSnapshot <= 0 THEN 1
                ELSE foodPlan.PortionMultiplierSnapshot
            END,
            foodPlan.TotalAmount = product.Price * CASE
                WHEN foodPlan.ChargeableDays < 1 THEN 1
                ELSE foodPlan.ChargeableDays
            END,
            foodPlan.InventoryQuantityDeducted = 0
        FROM dbo.HotelBookingFoodPlans AS foodPlan
        INNER JOIN dbo.Products AS product ON product.Sku = N''HOTEL-FOOD-DEFAULT''
        WHERE foodPlan.ProductSku IS NULL
           OR foodPlan.PlanType = N''OwnerProvided'';';
END;
GO

SELECT Code, Type, Size, Capacity, DailyPrice, Status
FROM dbo.RoomTypes
ORDER BY CASE Code WHEN N'STANDARD' THEN 1 WHEN N'VIP' THEN 2 WHEN N'LUXURY' THEN 3 ELSE 4 END,
         RoomTypeId;

SELECT
    OBJECT_ID(N'dbo.RoomMaintenanceLogs', N'U') AS RoomMaintenanceLogs,
    OBJECT_ID(N'dbo.HotelCageChangeRequests', N'U') AS HotelCageChangeRequests,
    OBJECT_ID(N'dbo.HotelCageStaySegments', N'U') AS HotelCageStaySegments,
    COL_LENGTH(N'dbo.RoomTypes', N'Code') AS RoomTypeCodeColumn;
GO
