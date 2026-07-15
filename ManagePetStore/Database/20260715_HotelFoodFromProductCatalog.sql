SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.ProductCategories', N'Code') IS NULL
    ALTER TABLE dbo.ProductCategories ADD Code NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.ProductCategories WHERE Code = N'HOTEL_STAY_FOOD')
BEGIN
    DECLARE @ExistingHotelFoodCategoryId INT = (
        SELECT MIN(CategoryId)
        FROM dbo.ProductCategories
        WHERE Name = N'Thức ăn cho Pet lưu trú');

    IF @ExistingHotelFoodCategoryId IS NULL
    BEGIN
        INSERT dbo.ProductCategories (Code, Name, Description, IsDeleted, Keywords)
        VALUES (
            N'HOTEL_STAY_FOOD',
            N'Thức ăn cho Pet lưu trú',
            N'Các suất thức ăn bắt buộc dùng cho dịch vụ Hotel; đơn vị kho là một pet-day.',
            0,
            N'hotel food,thuc an luu tru,pet day');
    END
    ELSE
    BEGIN
        UPDATE dbo.ProductCategories
        SET Code = N'HOTEL_STAY_FOOD', IsDeleted = 0
        WHERE CategoryId = @ExistingHotelFoodCategoryId;
    END;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_ProductCategories_Code'
      AND object_id = OBJECT_ID(N'dbo.ProductCategories'))
BEGIN
    CREATE UNIQUE INDEX UX_ProductCategories_Code
        ON dbo.ProductCategories(Code)
        WHERE Code IS NOT NULL;
END;

DECLARE @HotelFoodCategoryId INT = (
    SELECT TOP (1) CategoryId
    FROM dbo.ProductCategories
    WHERE Code = N'HOTEL_STAY_FOOD');

MERGE dbo.Products AS target
USING (VALUES
    (N'HOTEL-FOOD-DEFAULT', N'Gói thức ăn Default', 30000.00, N'Khẩu phần tiêu chuẩn mỗi ngày cho Pet lưu trú.'),
    (N'HOTEL-FOOD-PREMIUM', N'Gói thức ăn Premium', 55000.00, N'Khẩu phần cao cấp mỗi ngày cho Pet lưu trú.'),
    (N'HOTEL-FOOD-LUXURY', N'Gói thức ăn Luxury', 80000.00, N'Khẩu phần dinh dưỡng cao nhất mỗi ngày cho Pet lưu trú.')
) AS source(Sku, Name, Price, Description)
ON target.Sku = source.Sku
WHEN MATCHED THEN UPDATE SET
    target.CategoryId = @HotelFoodCategoryId,
    target.Unit = N'Suất/ngày',
    target.IsDeleted = 0,
    target.Description = COALESCE(target.Description, source.Description),
    target.AnimalType = COALESCE(target.AnimalType, N'Tất cả')
WHEN NOT MATCHED THEN
    INSERT (Sku, Name, CategoryId, Unit, Stock, MinStock, ExpiryDate, ShelfLocation,
            Price, CostPrice, ImageUrl, AnimalType, Description, IsDeleted)
    VALUES (source.Sku, source.Name, @HotelFoodCategoryId, N'Suất/ngày', 0, 5, NULL, NULL,
            source.Price, 0, NULL, N'Tất cả', source.Description, 0);

IF COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'ProductSku') IS NULL
    ALTER TABLE dbo.HotelBookingFoodPlans ADD ProductSku NVARCHAR(50) NULL;

IF COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'ProductUnitSnapshot') IS NULL
    ALTER TABLE dbo.HotelBookingFoodPlans ADD ProductUnitSnapshot NVARCHAR(30) NOT NULL
        CONSTRAINT DF_HotelBookingFoodPlans_ProductUnit DEFAULT N'Suất/ngày';

IF COL_LENGTH(N'dbo.HotelBookingFoodPlans', N'InventoryQuantityDeducted') IS NULL
    ALTER TABLE dbo.HotelBookingFoodPlans ADD InventoryQuantityDeducted INT NOT NULL
        CONSTRAINT DF_HotelBookingFoodPlans_InventoryDeducted DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_HotelBookingFoodPlans_Products')
    ALTER TABLE dbo.HotelBookingFoodPlans WITH CHECK
        ADD CONSTRAINT FK_HotelBookingFoodPlans_Products
        FOREIGN KEY (ProductSku) REFERENCES dbo.Products(Sku) ON DELETE SET NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_HotelBookingFoodPlans_ProductSku'
      AND object_id = OBJECT_ID(N'dbo.HotelBookingFoodPlans'))
    CREATE INDEX IX_HotelBookingFoodPlans_ProductSku
        ON dbo.HotelBookingFoodPlans(ProductSku);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_HotelBookingFoodPlans_InventoryDeducted')
    ALTER TABLE dbo.HotelBookingFoodPlans
        ADD CONSTRAINT CK_HotelBookingFoodPlans_InventoryDeducted
        CHECK (InventoryQuantityDeducted >= 0);

COMMIT TRANSACTION;
