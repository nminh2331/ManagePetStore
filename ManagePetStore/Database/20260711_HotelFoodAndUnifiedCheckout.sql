/* Run on PetStoreManagement after the existing database script. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.HotelBookings', N'U') IS NULL OR OBJECT_ID(N'dbo.Orders', N'U') IS NULL
BEGIN
    RAISERROR(N'Thiếu schema nền HotelBookings/Orders. Hãy chạy databasenew trước.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.HotelFoodOptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelFoodOptions (
        FoodOptionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelFoodOptions PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Description NVARCHAR(500) NULL,
        TargetSpecies NVARCHAR(30) NOT NULL CONSTRAINT DF_HotelFoodOptions_TargetSpecies DEFAULT N'Tất cả',
        PricePerDay DECIMAL(18,2) NOT NULL,
        DefaultPortionGrams INT NOT NULL,
        MealsPerDay INT NOT NULL,
        ImageUrl NVARCHAR(500) NULL,
        IsIncludedWithPremiumRoom BIT NOT NULL CONSTRAINT DF_HotelFoodOptions_Included DEFAULT 0,
        Active BIT NOT NULL CONSTRAINT DF_HotelFoodOptions_Active DEFAULT 1,
        ProductSku NVARCHAR(50) NULL,
        CONSTRAINT FK_HotelFoodOptions_Products FOREIGN KEY (ProductSku)
            REFERENCES dbo.Products(Sku) ON DELETE SET NULL,
        CONSTRAINT CK_HotelFoodOptions_Price CHECK (PricePerDay >= 0),
        CONSTRAINT CK_HotelFoodOptions_Portion CHECK (DefaultPortionGrams > 0),
        CONSTRAINT CK_HotelFoodOptions_Meals CHECK (MealsPerDay BETWEEN 1 AND 6)
    );
END;

IF OBJECT_ID(N'dbo.HotelBookingFoodPlans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelBookingFoodPlans (
        FoodPlanId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelBookingFoodPlans PRIMARY KEY,
        HotelBookingId INT NOT NULL,
        FoodOptionId INT NULL,
        PlanType NVARCHAR(30) NOT NULL,
        FoodNameSnapshot NVARCHAR(150) NOT NULL,
        PricePerDaySnapshot DECIMAL(18,2) NOT NULL,
        PortionGrams INT NOT NULL,
        MealsPerDay INT NOT NULL,
        FeedingInstructions NVARCHAR(1000) NULL,
        AllergyNotes NVARCHAR(1000) NULL,
        ChargeableDays INT NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_HotelBookingFoodPlans_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_HotelBookingFoodPlans_HotelBookings FOREIGN KEY (HotelBookingId)
            REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE CASCADE,
        CONSTRAINT FK_HotelBookingFoodPlans_HotelFoodOptions FOREIGN KEY (FoodOptionId)
            REFERENCES dbo.HotelFoodOptions(FoodOptionId) ON DELETE SET NULL,
        CONSTRAINT CK_HotelBookingFoodPlans_Amounts CHECK (PricePerDaySnapshot >= 0 AND TotalAmount >= 0)
    );
    CREATE UNIQUE INDEX UX_HotelBookingFoodPlans_HotelBookingId
        ON dbo.HotelBookingFoodPlans(HotelBookingId);
END;

IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'FoodPlanId') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD FoodPlanId INT NULL;
IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'MealType') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD MealType NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'ServedGrams') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD ServedGrams DECIMAL(10,2) NULL;
IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'ConsumedPercent') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD ConsumedPercent INT NULL;
IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'IsExtraCharge') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD IsExtraCharge BIT NOT NULL
        CONSTRAINT DF_FoodDiaryLogs_IsExtraCharge DEFAULT 0;
IF COL_LENGTH(N'dbo.FoodDiaryLogs', N'ExtraChargeAmount') IS NULL
    ALTER TABLE dbo.FoodDiaryLogs ADD ExtraChargeAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_FoodDiaryLogs_ExtraChargeAmount DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FoodDiaryLogs_HotelBookingFoodPlans')
    ALTER TABLE dbo.FoodDiaryLogs WITH CHECK ADD CONSTRAINT FK_FoodDiaryLogs_HotelBookingFoodPlans
        FOREIGN KEY (FoodPlanId) REFERENCES dbo.HotelBookingFoodPlans(FoodPlanId) ON DELETE NO ACTION;

IF OBJECT_ID(N'dbo.HotelCheckoutStatements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelCheckoutStatements (
        CheckoutStatementId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelCheckoutStatements PRIMARY KEY,
        HotelBookingId INT NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        CheckoutAt DATETIME NOT NULL,
        RoomAmount DECIMAL(18,2) NOT NULL,
        FoodAmount DECIMAL(18,2) NOT NULL,
        AddonAmount DECIMAL(18,2) NOT NULL,
        LateFeeAmount DECIMAL(18,2) NOT NULL,
        OtherAmount DECIMAL(18,2) NOT NULL,
        DiscountAmount DECIMAL(18,2) NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        PreparedByUserId INT NULL,
        PreparedByName NVARCHAR(100) NOT NULL,
        PreparedAt DATETIME NOT NULL,
        OrderId NVARCHAR(50) NULL,
        PaidAt DATETIME NULL,
        CONSTRAINT FK_HotelCheckoutStatements_HotelBookings FOREIGN KEY (HotelBookingId)
            REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE CASCADE,
        CONSTRAINT FK_HotelCheckoutStatements_Orders FOREIGN KEY (OrderId)
            REFERENCES dbo.Orders(OrderId) ON DELETE SET NULL,
        CONSTRAINT CK_HotelCheckoutStatements_Total CHECK (TotalAmount >= 0)
    );
    CREATE UNIQUE INDEX UX_HotelCheckoutStatements_HotelBookingId
        ON dbo.HotelCheckoutStatements(HotelBookingId);
    CREATE INDEX IX_HotelCheckoutStatements_Status_PreparedAt
        ON dbo.HotelCheckoutStatements(Status, PreparedAt);
END;

IF OBJECT_ID(N'dbo.HotelCheckoutItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelCheckoutItems (
        CheckoutItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelCheckoutItems PRIMARY KEY,
        CheckoutStatementId INT NOT NULL,
        ChargeType NVARCHAR(30) NOT NULL,
        Description NVARCHAR(250) NOT NULL,
        Quantity DECIMAL(10,2) NOT NULL,
        Unit NVARCHAR(30) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        SourceType NVARCHAR(30) NULL,
        SourceId NVARCHAR(50) NULL,
        CONSTRAINT FK_HotelCheckoutItems_HotelCheckoutStatements FOREIGN KEY (CheckoutStatementId)
            REFERENCES dbo.HotelCheckoutStatements(CheckoutStatementId) ON DELETE CASCADE,
        CONSTRAINT CK_HotelCheckoutItems_Amount CHECK (Quantity > 0 AND UnitPrice >= 0 AND Amount >= 0)
    );
    CREATE INDEX IX_HotelCheckoutItems_Statement
        ON dbo.HotelCheckoutItems(CheckoutStatementId);
END;

IF OBJECT_ID(N'dbo.HotelStaySpaLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelStaySpaLinks (
        HotelBookingId INT NOT NULL,
        SpaBookingId INT NOT NULL,
        LinkedAt DATETIME NOT NULL CONSTRAINT DF_HotelStaySpaLinks_LinkedAt DEFAULT GETDATE(),
        CONSTRAINT PK_HotelStaySpaLinks PRIMARY KEY (HotelBookingId, SpaBookingId),
        CONSTRAINT FK_HotelStaySpaLinks_HotelBookings FOREIGN KEY (HotelBookingId)
            REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE CASCADE,
        CONSTRAINT FK_HotelStaySpaLinks_SpaBookings FOREIGN KEY (SpaBookingId)
            REFERENCES dbo.SpaBookings(BookingId) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_HotelStaySpaLinks_SpaBookingId
        ON dbo.HotelStaySpaLinks(SpaBookingId);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.HotelFoodOptions)
BEGIN
    INSERT dbo.HotelFoodOptions
        (Name, Description, TargetSpecies, PricePerDay, DefaultPortionGrams, MealsPerDay, ImageUrl, IsIncludedWithPremiumRoom, Active)
    VALUES
        (N'Hạt tiêu chuẩn Hotel', N'Khẩu phần cân bằng dùng hằng ngày tại Hotel.', N'Tất cả', 30000, 80, 2, NULL, 0, 1),
        (N'Hạt Premium', N'Gói dinh dưỡng cao cấp, được bao gồm với phòng có tiện nghi Premium Food.', N'Tất cả', 55000, 90, 2, NULL, 1, 1),
        (N'Hạt Sensitive', N'Công thức nhẹ bụng dành cho pet cần theo dõi tiêu hóa.', N'Tất cả', 65000, 75, 2, NULL, 0, 1);
END;

INSERT dbo.HotelBookingFoodPlans
    (HotelBookingId, FoodOptionId, PlanType, FoodNameSnapshot, PricePerDaySnapshot,
     PortionGrams, MealsPerDay, FeedingInstructions, AllergyNotes, ChargeableDays, TotalAmount, CreatedAt)
SELECT booking.HotelBookingId, NULL, N'OwnerProvided', N'Chủ nuôi tự chuẩn bị', 0,
       0, 0, NULL, NULL, CASE WHEN booking.StayDays < 1 THEN 1 ELSE booking.StayDays END, 0, GETDATE()
FROM dbo.HotelBookings AS booking
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.HotelBookingFoodPlans AS food_plan
    WHERE food_plan.HotelBookingId = booking.HotelBookingId);

COMMIT TRANSACTION;
GO

SELECT
    OBJECT_ID(N'dbo.HotelFoodOptions', N'U') AS HotelFoodOptions,
    OBJECT_ID(N'dbo.HotelBookingFoodPlans', N'U') AS HotelBookingFoodPlans,
    OBJECT_ID(N'dbo.HotelCheckoutStatements', N'U') AS HotelCheckoutStatements,
    OBJECT_ID(N'dbo.HotelCheckoutItems', N'U') AS HotelCheckoutItems,
    OBJECT_ID(N'dbo.HotelStaySpaLinks', N'U') AS HotelStaySpaLinks,
    COL_LENGTH(N'dbo.FoodDiaryLogs', N'FoodPlanId') AS FoodPlanIdColumn;
GO
