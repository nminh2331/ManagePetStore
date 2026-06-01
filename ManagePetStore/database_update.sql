USE [PetStoreManagement]; -- Replace with your actual database name if different
GO

-- 1. Create ProductCategory table
CREATE TABLE [dbo].[ProductCategories] (
    [CategoryId] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    CONSTRAINT [PK_ProductCategories] PRIMARY KEY CLUSTERED ([CategoryId] ASC)
);
GO

-- 2. Insert existing distinct categories from Products table into ProductCategories
INSERT INTO [dbo].[ProductCategories] ([Name])
SELECT DISTINCT [Category]
FROM [dbo].[Products]
WHERE [Category] IS NOT NULL AND [Category] <> '';
GO

-- 3. Add CategoryId column to Products
ALTER TABLE [dbo].[Products]
ADD [CategoryId] INT NULL;
GO

-- 4. Map existing string categories to CategoryId
UPDATE P
SET P.[CategoryId] = PC.[CategoryId]
FROM [dbo].[Products] P
INNER JOIN [dbo].[ProductCategories] PC ON P.[Category] = PC.[Name];
GO

-- 5. Drop the old string Category column
ALTER TABLE [dbo].[Products]
DROP COLUMN [Category];
GO

-- 6. Add Foreign Key constraint to Products
ALTER TABLE [dbo].[Products]  WITH CHECK ADD  CONSTRAINT [FK_Products_ProductCategories] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[ProductCategories] ([CategoryId]);
GO

ALTER TABLE [dbo].[Products] CHECK CONSTRAINT [FK_Products_ProductCategories];
GO
