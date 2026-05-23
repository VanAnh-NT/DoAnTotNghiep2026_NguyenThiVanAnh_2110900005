using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Data;

public static class AppDataSeeder
{
    public static async Task SeedAsync(VanAnhPerfumeContext context, bool resetSeedData = false)
    {
        await context.Database.EnsureCreatedAsync();
        await EnsureAddToCartLogTableAsync(context);
        await EnsureAdminUpgradeSchemaAsync(context);
        if (resetSeedData)
        {
            await CleanupLegacySeedDataAsync(context);
        }
        var now = DateTime.UtcNow;

        await SeedRolesAsync(context);
        await SeedCategoriesAsync(context);
        await SeedBrandsAsync(context);
        await SeedUsersAsync(context);
        await SeedAddressBooksAsync(context);
        await SeedNewsAsync(context, now);
        await SeedProductsAsync(context);
        await EnsureProductHoverImagesAsync(context);
        await SeedPromotionsAsync(context, now);
        if (resetSeedData)
        {
            await SeedCouponsLegacyAsync(context, now);
        }
        await SeedNewsletterAsync(context);
        await SeedBannersAsync(context, now);
        await SeedOrdersAsync(context, now);

        await context.SaveChangesAsync();
    }

    private static async Task CleanupLegacySeedDataAsync(VanAnhPerfumeContext context)
    {
        var seedCustomerIds = await context.Users
            .Where(u => u.RoleId == 2 && EF.Functions.Like(u.Email, "%@vananh.test"))
            .Select(u => u.UserId)
            .ToListAsync();

        var seededProductIds = await context.Products
            .Where(p => EF.Functions.Like(p.Name, "Luxury Perfume%"))
            .Select(p => p.ProductId)
            .ToListAsync();

        var seededOrderIds = await context.Orders
            .Where(o => o.UserId.HasValue && seedCustomerIds.Contains(o.UserId.Value))
            .Select(o => o.OrderId)
            .ToListAsync();

        if (seededOrderIds.Count > 0)
        {
            await context.OrderStatusLogs
                .Where(x => seededOrderIds.Contains(x.OrderId))
                .ExecuteDeleteAsync();

            var seededPaymentIds = await context.Payments.AsNoTracking()
                .Where(x => seededOrderIds.Contains(x.OrderId))
                .Select(x => x.PaymentId)
                .ToListAsync();
            if (seededPaymentIds.Count > 0)
            {
                await context.PaymentLogs
                    .Where(x => seededPaymentIds.Contains(x.PaymentId))
                    .ExecuteDeleteAsync();
            }

            await context.Payments
                .Where(x => seededOrderIds.Contains(x.OrderId))
                .ExecuteDeleteAsync();
            await context.OrderDetails
                .Where(x => seededOrderIds.Contains(x.OrderId))
                .ExecuteDeleteAsync();
            await context.Orders
                .Where(x => seededOrderIds.Contains(x.OrderId))
                .ExecuteDeleteAsync();
        }

        if (seedCustomerIds.Count > 0)
        {
            await context.AddressBooks.Where(x => seedCustomerIds.Contains(x.UserId)).ExecuteDeleteAsync();
            await context.Reviews.Where(x => x.UserId.HasValue && seedCustomerIds.Contains(x.UserId.Value)).ExecuteDeleteAsync();
        }

        if (seededProductIds.Count > 0)
        {
            var variantIds = await context.ProductVariants
                .Where(v => seededProductIds.Contains(v.ProductId))
                .Select(v => v.VariantId)
                .ToListAsync();

            if (variantIds.Count > 0)
            {
                await context.OrderDetails
                    .Where(x => variantIds.Contains(x.VariantId))
                    .ExecuteDeleteAsync();

                await context.Carts.Where(x => variantIds.Contains(x.VariantId)).ExecuteDeleteAsync();
                await context.ProductVariantAttributeValues.Where(x => variantIds.Contains(x.ProductVariantId)).ExecuteDeleteAsync();
                await context.BatchInventories.Where(x => variantIds.Contains(x.VariantId)).ExecuteDeleteAsync();
                await context.InventoryLogs.Where(x => variantIds.Contains(x.VariantId)).ExecuteDeleteAsync();
                await context.Inventories.Where(x => variantIds.Contains(x.ProductVariantId)).ExecuteDeleteAsync();
            }

            await context.Reviews.Where(x => seededProductIds.Contains(x.ProductId)).ExecuteDeleteAsync();
            await context.ProductImages.Where(x => seededProductIds.Contains(x.ProductId)).ExecuteDeleteAsync();
            await context.ProductVariants.Where(x => seededProductIds.Contains(x.ProductId)).ExecuteDeleteAsync();
            await context.Products.Where(x => seededProductIds.Contains(x.ProductId)).ExecuteDeleteAsync();
        }

        await context.NewsletterSubscribers
            .Where(x => EF.Functions.Like(x.Email, "subscriber%@vananh.test"))
            .ExecuteDeleteAsync();
    }

    private static async Task EnsureAddToCartLogTableAsync(VanAnhPerfumeContext context)
    {
        const string sql = """
IF OBJECT_ID(N'dbo.AddToCartLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AddToCartLogs](
        [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductId] [int] NULL,
        [VariantId] [int] NULL,
        [Quantity] [int] NOT NULL CONSTRAINT [DF_AddToCartLogs_Quantity] DEFAULT ((1)),
        [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_AddToCartLogs_CreatedAt] DEFAULT (getdate())
    );
END
""";
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task EnsureAdminUpgradeSchemaAsync(VanAnhPerfumeContext context)
    {
        const string sql = """
IF COL_LENGTH('Products', 'ProductStatus') IS NULL ALTER TABLE Products ADD ProductStatus nvarchar(20) NULL CONSTRAINT DF_Products_ProductStatus DEFAULT ('Active');
IF COL_LENGTH('Products', 'Slug') IS NULL ALTER TABLE Products ADD Slug nvarchar(255) NULL;
IF COL_LENGTH('Products', 'MetaTitle') IS NULL ALTER TABLE Products ADD MetaTitle nvarchar(255) NULL;
IF COL_LENGTH('Products', 'MetaDescription') IS NULL ALTER TABLE Products ADD MetaDescription nvarchar(500) NULL;
IF COL_LENGTH('Products', 'RelatedProductIds') IS NULL ALTER TABLE Products ADD RelatedProductIds nvarchar(1000) NULL;
IF COL_LENGTH('Products', 'ShortDescription') IS NULL ALTER TABLE Products ADD ShortDescription nvarchar(1000) NULL;
IF COL_LENGTH('Products', 'DetailSpecsJson') IS NULL ALTER TABLE Products ADD DetailSpecsJson nvarchar(max) NULL;
IF COL_LENGTH('Products', 'CreatedAt') IS NULL ALTER TABLE Products ADD CreatedAt datetime NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (getdate());
IF COL_LENGTH('Products', 'UpdatedAt') IS NULL ALTER TABLE Products ADD UpdatedAt datetime NOT NULL CONSTRAINT DF_Products_UpdatedAt DEFAULT (getdate());
IF COL_LENGTH('Products', 'AverageRating') IS NULL ALTER TABLE Products ADD AverageRating decimal(3,1) NULL;
IF COL_LENGTH('Products', 'ReviewCount') IS NULL ALTER TABLE Products ADD ReviewCount int NOT NULL CONSTRAINT DF_Products_ReviewCount DEFAULT (0);

IF COL_LENGTH('Categories', 'ParentId') IS NULL ALTER TABLE Categories ADD ParentId int NULL;
IF COL_LENGTH('Categories', 'Slug') IS NULL ALTER TABLE Categories ADD Slug nvarchar(255) NULL;
IF COL_LENGTH('Categories', 'Description') IS NULL ALTER TABLE Categories ADD Description nvarchar(1000) NULL;
IF COL_LENGTH('Categories', 'ImageUrl') IS NULL ALTER TABLE Categories ADD ImageUrl nvarchar(500) NULL;
IF COL_LENGTH('Categories', 'SortOrder') IS NULL ALTER TABLE Categories ADD SortOrder int NOT NULL CONSTRAINT DF_Categories_SortOrder DEFAULT (0);
IF COL_LENGTH('Categories', 'CreatedAt') IS NULL ALTER TABLE Categories ADD CreatedAt datetime NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT (GETDATE());
IF COL_LENGTH('Categories', 'MetaTitle') IS NULL ALTER TABLE Categories ADD MetaTitle nvarchar(255) NULL;
IF COL_LENGTH('Categories', 'MetaDescription') IS NULL ALTER TABLE Categories ADD MetaDescription nvarchar(500) NULL;
IF COL_LENGTH('Categories', 'IsActive') IS NULL ALTER TABLE Categories ADD IsActive bit NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1);

IF COL_LENGTH('Brands', 'Slug') IS NULL ALTER TABLE Brands ADD Slug nvarchar(255) NULL;
IF COL_LENGTH('Brands', 'Description') IS NULL ALTER TABLE Brands ADD Description nvarchar(max) NULL;
IF COL_LENGTH('Brands', 'LogoUrl') IS NULL ALTER TABLE Brands ADD LogoUrl nvarchar(500) NULL;
IF COL_LENGTH('Brands', 'BannerUrl') IS NULL ALTER TABLE Brands ADD BannerUrl nvarchar(500) NULL;
IF COL_LENGTH('Brands', 'CountryOfOrigin') IS NULL ALTER TABLE Brands ADD CountryOfOrigin nvarchar(100) NULL;
IF COL_LENGTH('Brands', 'Website') IS NULL ALTER TABLE Brands ADD Website nvarchar(255) NULL;
IF COL_LENGTH('Brands', 'SortOrder') IS NULL ALTER TABLE Brands ADD SortOrder int NOT NULL CONSTRAINT DF_Brands_SortOrder DEFAULT (0);
IF COL_LENGTH('Brands', 'CreatedAt') IS NULL ALTER TABLE Brands ADD CreatedAt datetime NOT NULL CONSTRAINT DF_Brands_CreatedAt DEFAULT (GETDATE());
IF COL_LENGTH('Brands', 'MetaTitle') IS NULL ALTER TABLE Brands ADD MetaTitle nvarchar(255) NULL;
IF COL_LENGTH('Brands', 'MetaDescription') IS NULL ALTER TABLE Brands ADD MetaDescription nvarchar(500) NULL;
IF COL_LENGTH('Brands', 'IsActive') IS NULL ALTER TABLE Brands ADD IsActive bit NOT NULL CONSTRAINT DF_Brands_IsActive DEFAULT (1);

IF COL_LENGTH('ProductVariants', 'Color') IS NULL ALTER TABLE ProductVariants ADD Color nvarchar(50) NULL;
IF COL_LENGTH('ProductVariants', 'Material') IS NULL ALTER TABLE ProductVariants ADD Material nvarchar(100) NULL;
IF COL_LENGTH('ProductVariants', 'VariantImageUrl') IS NULL ALTER TABLE ProductVariants ADD VariantImageUrl nvarchar(500) NULL;
IF COL_LENGTH('ProductVariants', 'OriginalPrice') IS NULL ALTER TABLE ProductVariants ADD OriginalPrice decimal(18,2) NULL;
IF COL_LENGTH('ProductVariants', 'IsActive') IS NULL ALTER TABLE ProductVariants ADD IsActive bit NOT NULL CONSTRAINT DF_ProductVariants_IsActive DEFAULT (1);
IF COL_LENGTH('ProductVariants', 'CreatedAt') IS NULL ALTER TABLE ProductVariants ADD CreatedAt datetime NOT NULL CONSTRAINT DF_ProductVariants_CreatedAt DEFAULT (getdate());

IF COL_LENGTH('ProductImages', 'CreatedAt') IS NULL ALTER TABLE ProductImages ADD CreatedAt datetime NOT NULL CONSTRAINT DF_ProductImages_CreatedAt DEFAULT (getdate());

IF COL_LENGTH('Orders', 'ShippingStatus') IS NULL ALTER TABLE Orders ADD ShippingStatus nvarchar(50) NULL CONSTRAINT DF_Orders_ShippingStatus DEFAULT ('Pending');
IF COL_LENGTH('Orders', 'TrackingCode') IS NULL ALTER TABLE Orders ADD TrackingCode nvarchar(100) NULL;
IF COL_LENGTH('Orders', 'AdminNote') IS NULL ALTER TABLE Orders ADD AdminNote nvarchar(1000) NULL;
IF COL_LENGTH('Orders', 'RefundStatus') IS NULL ALTER TABLE Orders ADD RefundStatus nvarchar(50) NULL;
IF COL_LENGTH('Orders', 'OrderCode') IS NULL ALTER TABLE Orders ADD OrderCode nvarchar(30) NULL;
IF COL_LENGTH('Orders', 'CustomerEmail') IS NULL ALTER TABLE Orders ADD CustomerEmail nvarchar(150) NULL;
IF COL_LENGTH('Orders', 'ShippingProvince') IS NULL ALTER TABLE Orders ADD ShippingProvince nvarchar(100) NULL;
IF COL_LENGTH('Orders', 'ShippingDistrict') IS NULL ALTER TABLE Orders ADD ShippingDistrict nvarchar(100) NULL;
IF COL_LENGTH('Orders', 'SubTotal') IS NULL ALTER TABLE Orders ADD SubTotal decimal(18,2) NOT NULL CONSTRAINT DF_Orders_SubTotal DEFAULT (0);
IF COL_LENGTH('Orders', 'DiscountAmount') IS NULL ALTER TABLE Orders ADD DiscountAmount decimal(18,2) NOT NULL CONSTRAINT DF_Orders_DiscountAmount DEFAULT (0);
IF COL_LENGTH('Orders', 'ShippingFee') IS NULL ALTER TABLE Orders ADD ShippingFee decimal(18,2) NOT NULL CONSTRAINT DF_Orders_ShippingFee DEFAULT (0);
IF COL_LENGTH('Orders', 'CouponId') IS NULL ALTER TABLE Orders ADD CouponId int NULL;
IF COL_LENGTH('Orders', 'CouponCode') IS NULL ALTER TABLE Orders ADD CouponCode nvarchar(50) NULL;
IF COL_LENGTH('Orders', 'Note') IS NULL ALTER TABLE Orders ADD Note nvarchar(1000) NULL;
IF COL_LENGTH('Orders', 'UpdatedAt') IS NULL ALTER TABLE Orders ADD UpdatedAt datetime NOT NULL CONSTRAINT DF_Orders_UpdatedAt DEFAULT (getdate());
IF COL_LENGTH('Orders', 'ConfirmationEmailSentAt') IS NULL ALTER TABLE Orders ADD ConfirmationEmailSentAt datetime NULL;
IF COL_LENGTH('Orders', 'OrderCode') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Orders_OrderCode' AND object_id=OBJECT_ID('Orders')) EXEC('CREATE UNIQUE INDEX IX_Orders_OrderCode ON Orders(OrderCode) WHERE OrderCode IS NOT NULL');
IF COL_LENGTH('Orders', 'OrderCode') IS NOT NULL EXEC(N'UPDATE Orders SET OrderCode = CONCAT(''ORD-'', FORMAT(COALESCE(OrderDate, GETDATE()), ''yyyyMMdd''), ''-'', RIGHT(CONCAT(''00000'', OrderId), 5)) WHERE OrderCode IS NULL');

IF COL_LENGTH('OrderDetails', 'ProductName') IS NULL ALTER TABLE OrderDetails ADD ProductName nvarchar(255) NULL;
IF COL_LENGTH('OrderDetails', 'VariantName') IS NULL ALTER TABLE OrderDetails ADD VariantName nvarchar(255) NULL;
IF COL_LENGTH('OrderDetails', 'SKU') IS NULL ALTER TABLE OrderDetails ADD SKU nvarchar(100) NULL;

IF COL_LENGTH('Payments', 'TransactionId') IS NULL ALTER TABLE Payments ADD TransactionId nvarchar(100) NULL;
IF COL_LENGTH('Payments', 'PaymentGatewayResponse') IS NULL ALTER TABLE Payments ADD PaymentGatewayResponse nvarchar(4000) NULL;
IF COL_LENGTH('Payments', 'RefundedAt') IS NULL ALTER TABLE Payments ADD RefundedAt datetime NULL;
IF COL_LENGTH('Payments', 'RefundAmount') IS NULL ALTER TABLE Payments ADD RefundAmount decimal(18,2) NULL;
IF COL_LENGTH('Payments', 'RefundNote') IS NULL ALTER TABLE Payments ADD RefundNote nvarchar(500) NULL;
IF COL_LENGTH('Payments', 'UpdatedAt') IS NULL ALTER TABLE Payments ADD UpdatedAt datetime NULL;

IF OBJECT_ID(N'dbo.Coupons', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Coupons', 'PerUserLimit') IS NULL ALTER TABLE Coupons ADD PerUserLimit int NULL;
    IF COL_LENGTH('Coupons', 'StartDate') IS NULL ALTER TABLE Coupons ADD StartDate datetime NULL;
    IF COL_LENGTH('Coupons', 'ApplicableCategoryIds') IS NULL ALTER TABLE Coupons ADD ApplicableCategoryIds nvarchar(500) NULL;
    IF COL_LENGTH('Coupons', 'ApplicableProductIds') IS NULL ALTER TABLE Coupons ADD ApplicableProductIds nvarchar(1000) NULL;
    IF COL_LENGTH('Coupons', 'AutoApply') IS NULL ALTER TABLE Coupons ADD AutoApply bit NOT NULL CONSTRAINT DF_Coupons_AutoApply DEFAULT (0);
END

IF COL_LENGTH('Promotions', 'MinOrderValue') IS NULL ALTER TABLE Promotions ADD MinOrderValue decimal(18,2) NULL;
IF COL_LENGTH('Promotions', 'ApplicableCategoryIds') IS NULL ALTER TABLE Promotions ADD ApplicableCategoryIds nvarchar(500) NULL;
IF COL_LENGTH('Promotions', 'ApplicableProductIds') IS NULL ALTER TABLE Promotions ADD ApplicableProductIds nvarchar(1000) NULL;
IF COL_LENGTH('Promotions', 'AutoApply') IS NULL ALTER TABLE Promotions ADD AutoApply bit NOT NULL CONSTRAINT DF_Promotions_AutoApply DEFAULT (0);

IF COL_LENGTH('News', 'MetaTitle') IS NULL ALTER TABLE News ADD MetaTitle nvarchar(255) NULL;
IF COL_LENGTH('News', 'MetaDescription') IS NULL ALTER TABLE News ADD MetaDescription nvarchar(500) NULL;
IF COL_LENGTH('News', 'PublishAt') IS NULL ALTER TABLE News ADD PublishAt datetime NULL;
IF COL_LENGTH('News', 'Excerpt') IS NULL ALTER TABLE News ADD Excerpt nvarchar(500) NULL;
IF COL_LENGTH('News', 'AuthorName') IS NULL ALTER TABLE News ADD AuthorName nvarchar(200) NULL;
IF COL_LENGTH('News', 'CategoryTag') IS NULL ALTER TABLE News ADD CategoryTag nvarchar(100) NULL;
IF COL_LENGTH('News', 'ViewCount') IS NULL ALTER TABLE News ADD ViewCount int NOT NULL CONSTRAINT DF_News_ViewCount DEFAULT (0);
IF COL_LENGTH('News', 'IsFeatured') IS NULL ALTER TABLE News ADD IsFeatured bit NOT NULL CONSTRAINT DF_News_IsFeatured DEFAULT (0);
IF COL_LENGTH('News', 'UpdatedAt') IS NULL ALTER TABLE News ADD UpdatedAt datetime NOT NULL CONSTRAINT DF_News_UpdatedAt DEFAULT (GETDATE());

IF OBJECT_ID(N'dbo.Banners', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Banners', 'MetaTitle') IS NULL ALTER TABLE Banners ADD MetaTitle nvarchar(255) NULL;
    IF COL_LENGTH('Banners', 'MetaDescription') IS NULL ALTER TABLE Banners ADD MetaDescription nvarchar(500) NULL;
    IF COL_LENGTH('Banners', 'PublishAt') IS NULL ALTER TABLE Banners ADD PublishAt datetime NULL;
    IF COL_LENGTH('Banners', 'MobileImageUrl') IS NULL ALTER TABLE Banners ADD MobileImageUrl nvarchar(500) NULL;
    IF COL_LENGTH('Banners', 'Description') IS NULL ALTER TABLE Banners ADD Description nvarchar(1000) NULL;
    IF COL_LENGTH('Banners', 'ButtonText') IS NULL ALTER TABLE Banners ADD ButtonText nvarchar(100) NULL CONSTRAINT DF_Banners_ButtonText DEFAULT ('Khám phá ngay');
    IF COL_LENGTH('Banners', 'TextColor') IS NULL ALTER TABLE Banners ADD TextColor nvarchar(7) NULL CONSTRAINT DF_Banners_TextColor DEFAULT ('#FFFFFF');
    IF COL_LENGTH('Banners', 'Position') IS NULL ALTER TABLE Banners ADD Position nvarchar(50) NULL CONSTRAINT DF_Banners_Position DEFAULT ('hero');
    IF COL_LENGTH('Banners', 'CreatedAt') IS NULL ALTER TABLE Banners ADD CreatedAt datetime NOT NULL CONSTRAINT DF_Banners_CreatedAt DEFAULT (GETDATE());
END

IF OBJECT_ID(N'dbo.InventoryLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InventoryLogs](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [VariantId] [int] NOT NULL,
      [ActionType] [nvarchar](30) NOT NULL,
      [QuantityDelta] [int] NOT NULL,
      [StockAfter] [int] NOT NULL,
      [Note] [nvarchar](500) NULL,
      [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_InventoryLogs_CreatedAt] DEFAULT (getdate())
    );
END
IF COL_LENGTH('InventoryLogs', 'ChangeType') IS NULL ALTER TABLE InventoryLogs ADD ChangeType nvarchar(30) NOT NULL CONSTRAINT DF_InventoryLogs_ChangeType DEFAULT ('Adjustment');
IF COL_LENGTH('InventoryLogs', 'QuantityChange') IS NULL ALTER TABLE InventoryLogs ADD QuantityChange int NOT NULL CONSTRAINT DF_InventoryLogs_QuantityChange DEFAULT (0);
IF COL_LENGTH('InventoryLogs', 'QuantityAfter') IS NULL ALTER TABLE InventoryLogs ADD QuantityAfter int NOT NULL CONSTRAINT DF_InventoryLogs_QuantityAfter DEFAULT (0);
IF COL_LENGTH('InventoryLogs', 'CreatedBy') IS NULL ALTER TABLE InventoryLogs ADD CreatedBy nvarchar(100) NULL;

IF OBJECT_ID(N'dbo.VariantAttributes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[VariantAttributes](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [ProductId] [int] NOT NULL,
      [Name] [nvarchar](100) NOT NULL
    );
END

IF OBJECT_ID(N'dbo.VariantAttributeValues', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[VariantAttributeValues](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [VariantAttributeId] [int] NOT NULL,
      [Value] [nvarchar](100) NOT NULL
    );
END

IF OBJECT_ID(N'dbo.ProductVariantAttributeValues', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductVariantAttributeValues](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [ProductVariantId] [int] NOT NULL,
      [VariantAttributeValueId] [int] NOT NULL
    );
END

IF OBJECT_ID(N'dbo.Inventories', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Inventories](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [ProductVariantId] [int] NOT NULL UNIQUE,
      [QuantityAvailable] [int] NOT NULL CONSTRAINT DF_Inventories_QuantityAvailable DEFAULT (0),
      [QuantityReserved] [int] NOT NULL CONSTRAINT DF_Inventories_QuantityReserved DEFAULT (0),
      [QuantitySold] [int] NOT NULL CONSTRAINT DF_Inventories_QuantitySold DEFAULT (0),
      [LowStockThreshold] [int] NOT NULL CONSTRAINT DF_Inventories_LowStockThreshold DEFAULT (5),
      [UpdatedAt] [datetime] NOT NULL CONSTRAINT DF_Inventories_UpdatedAt DEFAULT (getdate())
    );
END

IF OBJECT_ID(N'dbo.OrderStatusLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderStatusLogs](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [OrderId] [int] NOT NULL,
      [OldStatus] [nvarchar](50) NULL,
      [NewStatus] [nvarchar](50) NULL,
      [OldPaymentStatus] [nvarchar](50) NULL,
      [NewPaymentStatus] [nvarchar](50) NULL,
      [Note] [nvarchar](500) NULL,
      [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_OrderStatusLogs_CreatedAt] DEFAULT (getdate())
    );
END
IF COL_LENGTH('OrderStatusLogs', 'ChangedBy') IS NULL ALTER TABLE OrderStatusLogs ADD ChangedBy nvarchar(100) NULL;

IF COL_LENGTH('PaymentLogs', 'OrderId') IS NULL ALTER TABLE PaymentLogs ADD OrderId int NULL;
IF COL_LENGTH('PaymentLogs', 'OldStatus') IS NULL ALTER TABLE PaymentLogs ADD OldStatus nvarchar(30) NULL;
IF COL_LENGTH('PaymentLogs', 'NewStatus') IS NULL ALTER TABLE PaymentLogs ADD NewStatus nvarchar(30) NULL;
IF COL_LENGTH('PaymentLogs', 'Note') IS NULL ALTER TABLE PaymentLogs ADD Note nvarchar(500) NULL;
IF COL_LENGTH('PaymentLogs', 'Source') IS NULL ALTER TABLE PaymentLogs ADD Source nvarchar(30) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Payments_OrderId' AND object_id=OBJECT_ID('Payments'))
BEGIN
    IF NOT EXISTS (SELECT OrderId FROM Payments GROUP BY OrderId HAVING COUNT(*) > 1)
        EXEC('CREATE UNIQUE INDEX UX_Payments_OrderId ON Payments(OrderId)');
END

IF COL_LENGTH('Reviews', 'ReviewerName') IS NULL ALTER TABLE Reviews ADD ReviewerName nvarchar(200) NULL;
IF COL_LENGTH('Reviews', 'ReviewerEmail') IS NULL ALTER TABLE Reviews ADD ReviewerEmail nvarchar(200) NULL;
IF COL_LENGTH('Reviews', 'Title') IS NULL ALTER TABLE Reviews ADD Title nvarchar(200) NULL;
IF COL_LENGTH('Reviews', 'Content') IS NULL ALTER TABLE Reviews ADD Content nvarchar(2000) NULL;
IF COL_LENGTH('Reviews', 'Status') IS NULL ALTER TABLE Reviews ADD Status nvarchar(20) NOT NULL CONSTRAINT DF_Reviews_Status DEFAULT ('Pending');
IF COL_LENGTH('Reviews', 'IsVerifiedPurchase') IS NULL ALTER TABLE Reviews ADD IsVerifiedPurchase bit NOT NULL CONSTRAINT DF_Reviews_IsVerifiedPurchase DEFAULT (0);
IF COL_LENGTH('Reviews', 'AdminReply') IS NULL ALTER TABLE Reviews ADD AdminReply nvarchar(1000) NULL;
IF COL_LENGTH('Reviews', 'AdminReplyAt') IS NULL ALTER TABLE Reviews ADD AdminReplyAt datetime NULL;
IF COL_LENGTH('Reviews', 'HelpfulCount') IS NULL ALTER TABLE Reviews ADD HelpfulCount int NOT NULL CONSTRAINT DF_Reviews_HelpfulCount DEFAULT (0);
IF COL_LENGTH('Reviews', 'UpdatedAt') IS NULL ALTER TABLE Reviews ADD UpdatedAt datetime NOT NULL CONSTRAINT DF_Reviews_UpdatedAt DEFAULT (getdate());
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Reviews_User_Product' AND object_id=OBJECT_ID('Reviews'))
BEGIN
    DROP INDEX UX_Reviews_User_Product ON Reviews;
END
IF COL_LENGTH('Reviews', 'UserId') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Reviews_Users')
BEGIN
    ALTER TABLE Reviews DROP CONSTRAINT FK_Reviews_Users;
    ALTER TABLE Reviews ALTER COLUMN UserId int NULL;
    ALTER TABLE Reviews WITH CHECK ADD CONSTRAINT FK_Reviews_Users FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE SET NULL;
END
ELSE IF COL_LENGTH('Reviews', 'UserId') IS NOT NULL
BEGIN
    ALTER TABLE Reviews ALTER COLUMN UserId int NULL;
END
IF COL_LENGTH('Reviews', 'UserId') IS NOT NULL
    EXEC('CREATE UNIQUE INDEX UX_Reviews_User_Product ON Reviews(UserId, ProductId) WHERE UserId IS NOT NULL');

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLogs](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [EntityType] [nvarchar](100) NOT NULL,
      [EntityId] [int] NOT NULL,
      [ActionType] [nvarchar](50) NOT NULL,
      [ChangedBy] [nvarchar](150) NULL,
      [Payload] [nvarchar](max) NULL,
      [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_AuditLogs_CreatedAt] DEFAULT (getdate())
    );
END

IF OBJECT_ID(N'dbo.AdminNotifications', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AdminNotifications](
      [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
      [Title] [nvarchar](255) NOT NULL,
      [Message] [nvarchar](1000) NOT NULL,
      [Type] [nvarchar](30) NOT NULL CONSTRAINT [DF_AdminNotifications_Type] DEFAULT ('Warning'),
      [IsRead] [bit] NOT NULL CONSTRAINT [DF_AdminNotifications_IsRead] DEFAULT (0),
      [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_AdminNotifications_CreatedAt] DEFAULT (getdate())
    );
END

IF COL_LENGTH('Users', 'Avatar') IS NULL ALTER TABLE Users ADD Avatar nvarchar(500) NULL;
IF COL_LENGTH('Users', 'DateOfBirth') IS NULL ALTER TABLE Users ADD DateOfBirth datetime NULL;
IF COL_LENGTH('Users', 'Gender') IS NULL ALTER TABLE Users ADD Gender nvarchar(10) NULL;
IF COL_LENGTH('Users', 'IsActive') IS NULL ALTER TABLE Users ADD IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1);
IF COL_LENGTH('Users', 'IsEmailVerified') IS NULL ALTER TABLE Users ADD IsEmailVerified bit NOT NULL CONSTRAINT DF_Users_IsEmailVerified DEFAULT (0);
IF COL_LENGTH('Users', 'LastLoginAt') IS NULL ALTER TABLE Users ADD LastLoginAt datetime NULL;
IF COL_LENGTH('Users', 'UpdatedAt') IS NULL ALTER TABLE Users ADD UpdatedAt datetime NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT (getdate());
IF COL_LENGTH('AddressBook', 'CreatedAt') IS NULL ALTER TABLE AddressBook ADD CreatedAt datetime NOT NULL CONSTRAINT DF_AddressBook_CreatedAt DEFAULT (getdate());
""";
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task SeedRolesAsync(VanAnhPerfumeContext context)
    {
        var expected = new[] { "Admin", "Customer" };
        var existing = await context.Roles.Select(x => x.RoleName).ToListAsync();
        var missing = expected.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.Roles.AddRange(missing.Select(x => new Role { RoleName = x }));
        await context.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(VanAnhPerfumeContext context)
    {
        var topLevel = new[]
        {
            "Nước hoa nam", "Nước hoa nữ", "Nước hoa unisex", "Nước hoa niche", "Bodycare", "Nến thơm", "Tinh dầu thơm"
        };
        foreach (var name in topLevel)
        {
            var category = await context.Categories.FirstOrDefaultAsync(x => x.Name == name);
            if (category == null)
            {
                category = new Category { Name = name };
                context.Categories.Add(category);
            }

            category.ParentId = null;
            category.IsActive = true;
            category.Slug = ToSlug(name);
            category.MetaTitle = $"{name} | Vân Anh Perfume";
            category.MetaDescription = $"Bộ sưu tập {name} cao cấp được tuyển chọn tại Vân Anh Perfume.";
        }
        await context.SaveChangesAsync();

        var parentMap = await context.Categories
            .Where(x => x.ParentId == null)
            .ToDictionaryAsync(x => x.Name, x => x.CategoryId);
        var children = new (string Name, string Parent)[]
        {
            ("Hương gỗ nam tính", "Nước hoa nam"),
            ("Hương biển năng động", "Nước hoa nam"),
            ("Hương hoa thanh lịch", "Nước hoa nữ"),
            ("Hương ngọt quyến rũ", "Nước hoa nữ"),
            ("Unisex văn phòng", "Nước hoa unisex"),
            ("Unisex mùa hè", "Nước hoa unisex"),
            ("Sữa tắm hương nước hoa", "Bodycare"),
            ("Dưỡng thể thơm lâu", "Bodycare")
        };

        foreach (var item in children)
        {
            if (!parentMap.TryGetValue(item.Parent, out var parentId))
            {
                continue;
            }
            var category = await context.Categories.FirstOrDefaultAsync(x => x.Name == item.Name);
            if (category == null)
            {
                category = new Category { Name = item.Name };
                context.Categories.Add(category);
            }
            category.ParentId = parentId;
            category.IsActive = true;
            category.Slug = ToSlug(item.Name);
            category.MetaTitle = $"{item.Name} | Vân Anh Perfume";
            category.MetaDescription = $"Danh mục {item.Name} thuộc {item.Parent}.";
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedBrandsAsync(VanAnhPerfumeContext context)
    {
        var expected = new (string Name, string Country, string? Logo)[]
        {
            ("Gucci", "Italy", "https://upload.wikimedia.org/wikipedia/commons/7/79/1960s_Gucci_Logo.svg"),
            ("Dior", "France", "https://upload.wikimedia.org/wikipedia/commons/3/3f/Christian_Dior_SE_logo.svg"),
            ("Chanel", "France", "https://upload.wikimedia.org/wikipedia/commons/8/81/Chanel_Logo.svg"),
            ("Tom Ford", "USA", "https://upload.wikimedia.org/wikipedia/commons/f/f4/Tom_Ford_logo.svg"),
            ("Le Labo", "France", null),
            ("Jo Malone", "UK", null),
            ("Byredo", "Sweden", null),
            ("Creed", "France", null),
            ("Maison Francis Kurkdjian", "France", null),
            ("YSL", "France", null)
        };

        foreach (var item in expected)
        {
            var brand = await context.Brands.FirstOrDefaultAsync(x => x.Name == item.Name);
            if (brand == null)
            {
                brand = new Brand { Name = item.Name };
                context.Brands.Add(brand);
            }
            brand.Country = item.Country;
            brand.Logo = item.Logo;
            brand.IsActive = true;
            brand.Slug = ToSlug(item.Name);
            brand.MetaTitle = $"{item.Name} Perfume | Vân Anh Perfume";
            brand.MetaDescription = $"Nước hoa {item.Name} chính hãng, đa dạng nồng độ và dung tích.";
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(VanAnhPerfumeContext context)
    {
        var adminRoleId = await context.Roles.Where(x => x.RoleName == "Admin").Select(x => x.RoleId).FirstAsync();
        var customerRoleId = await context.Roles.Where(x => x.RoleName == "Customer").Select(x => x.RoleId).FirstAsync();

        if (!await context.Users.AnyAsync(x => x.Email == "admin@vananh.test"))
        {
            var aNow = DateTime.UtcNow;
            context.Users.Add(new User
            {
                FullName = "System Admin",
                Email = "admin@vananh.test",
                Password = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Phone = "0900000001",
                RoleId = adminRoleId,
                CreatedAt = aNow,
                UpdatedAt = aNow,
                Status = true,
                IsActive = true
            });
        }

        var defaultCustomer = await context.Users.FirstOrDefaultAsync(x => x.Email == "customer@vananh.test");
        if (defaultCustomer == null)
        {
            var cNow = DateTime.UtcNow;
            context.Users.Add(new User
            {
                FullName = "Test Customer",
                Email = "customer@vananh.test",
                Password = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                Phone = "0900000002",
                RoleId = customerRoleId,
                CreatedAt = cNow,
                UpdatedAt = cNow,
                Status = true,
                IsActive = true
            });
        }
        else
        {
            defaultCustomer.FullName = "Test Customer";
            defaultCustomer.Phone = "0900000002";
            defaultCustomer.UpdatedAt = DateTime.UtcNow;
            defaultCustomer.LastLoginAt ??= DateTime.UtcNow.AddDays(-1);
            defaultCustomer.DateOfBirth ??= new DateTime(1995, 5, 20);
            defaultCustomer.Gender ??= "Female";
            defaultCustomer.Avatar ??= "https://picsum.photos/id/1027/400/400";
            defaultCustomer.IsEmailVerified = true;
            defaultCustomer.Status = true;
            defaultCustomer.IsActive = true;
        }

        var avatarPool = new[]
        {
            "https://picsum.photos/id/1005/400/400",
            "https://picsum.photos/id/1011/400/400",
            "https://picsum.photos/id/1027/400/400",
            "https://picsum.photos/id/1025/400/400",
            "https://picsum.photos/id/1074/400/400",
            "https://picsum.photos/id/177/400/400"
        };

        for (var i = 1; i <= 20; i++)
        {
            var email = $"customer{i:00}@vananh.test";
            var uAt = DateTime.UtcNow.AddDays(-i);
            var existing = await context.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (existing == null)
            {
                context.Users.Add(new User
                {
                    FullName = $"Khach Hang {i:00}",
                    Email = email,
                    Password = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                    Phone = $"09123{i:0000}",
                    RoleId = customerRoleId,
                    CreatedAt = uAt,
                    UpdatedAt = uAt,
                    LastLoginAt = uAt.AddDays(i % 5),
                    DateOfBirth = new DateTime(1990 + (i % 10), 1 + (i % 12), 1 + (i % 27)),
                    Gender = i % 3 == 0 ? "Unisex" : (i % 2 == 0 ? "Female" : "Male"),
                    Avatar = avatarPool[i % avatarPool.Length],
                    IsEmailVerified = i % 4 != 0,
                    Status = true,
                    IsActive = i % 9 != 0
                });
                continue;
            }

            existing.FullName = $"Khach Hang {i:00}";
            existing.Phone = $"09123{i:0000}";
            existing.RoleId = customerRoleId;
            existing.CreatedAt ??= uAt;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.LastLoginAt ??= uAt.AddDays(i % 5);
            existing.DateOfBirth ??= new DateTime(1990 + (i % 10), 1 + (i % 12), 1 + (i % 27));
            existing.Gender ??= i % 3 == 0 ? "Unisex" : (i % 2 == 0 ? "Female" : "Male");
            existing.Avatar ??= avatarPool[i % avatarPool.Length];
            existing.IsEmailVerified = i % 4 != 0;
            existing.Status = true;
            existing.IsActive = i % 9 != 0;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedAddressBooksAsync(VanAnhPerfumeContext context)
    {
        var customers = await context.Users
            .Where(x => x.Role.RoleName == "Customer")
            .OrderBy(x => x.UserId)
            .ToListAsync();

        if (customers.Count == 0)
        {
            return;
        }

        var provinces = new[]
        {
            ("TP.HCM", "Quận 1", "Bến Nghé"),
            ("Hà Nội", "Ba Đình", "Liễu Giai"),
            ("Đà Nẵng", "Hải Châu", "Thạch Thang"),
            ("Cần Thơ", "Ninh Kiều", "An Hòa")
        };

        foreach (var customer in customers)
        {
            if (await context.AddressBooks.AnyAsync(a => a.UserId == customer.UserId))
            {
                continue;
            }

            var p1 = provinces[customer.UserId % provinces.Length];
            var p2 = provinces[(customer.UserId + 1) % provinces.Length];

            context.AddressBooks.AddRange(
                new AddressBook
                {
                    UserId = customer.UserId,
                    ReceiverName = customer.FullName,
                    Phone = customer.Phone ?? "0900000000",
                    StreetAddress = $"{10 + customer.UserId} Nguyen Hue",
                    Ward = p1.Item3,
                    District = p1.Item2,
                    City = p1.Item1,
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-customer.UserId)
                },
                new AddressBook
                {
                    UserId = customer.UserId,
                    ReceiverName = customer.FullName,
                    Phone = customer.Phone ?? "0900000000",
                    StreetAddress = $"{80 + customer.UserId} Tran Hung Dao",
                    Ward = p2.Item3,
                    District = p2.Item2,
                    City = p2.Item1,
                    IsDefault = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-customer.UserId + 1)
                });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedNewsAsync(VanAnhPerfumeContext context, DateTime now)
    {
        var newsSeed = new[]
        {
            new
            {
                Title = "Top mùi hương công sở thanh lịch 2026",
                Slug = "top-mui-huong-cong-so-thanh-lich-2026",
                ThumbnailUrl = "https://images.unsplash.com/photo-1541643600914-78b084683601?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Khám phá các mùi hương dễ dùng mỗi ngày, lưu hương ổn định và phù hợp môi trường công sở.</p><p>Gợi ý ưu tiên nhóm hương citrus, floral nhẹ và musk sạch để tạo cảm giác chỉn chu nhưng không quá gắt.</p>"
            },
            new
            {
                Title = "Hướng dẫn chọn nước hoa theo mùa tại Việt Nam",
                Slug = "huong-dan-chon-nuoc-hoa-theo-mua-viet-nam",
                ThumbnailUrl = "https://images.unsplash.com/photo-1594035910387-fea47794261f?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Khí hậu nóng ẩm cần ưu tiên cấu trúc hương thoáng, tươi và ít ngọt vào ban ngày.</p><p>Mùa mưa hoặc buổi tối có thể chuyển sang woody, amber hoặc gourmand để tăng chiều sâu.</p>"
            },
            new
            {
                Title = "Top 7 mùi hương unisex bán chạy tháng này",
                Slug = "top-7-mui-huong-unisex-ban-chay-thang-nay",
                ThumbnailUrl = "https://images.unsplash.com/photo-1610465299993-e6675c9f9efa?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Danh sách unisex nổi bật với độ cân bằng tốt giữa độ bám tỏa, tính ứng dụng và độ nhận diện thương hiệu.</p><p>Mỗi lựa chọn đều phù hợp từ đi làm đến đi chơi cuối tuần.</p>"
            },
            new
            {
                Title = "Nghệ thuật layering để tạo mùi hương cá nhân",
                Slug = "nghe-thuat-layering-tao-mui-huong-ca-nhan",
                ThumbnailUrl = "https://images.unsplash.com/photo-1615634262417-44b37c6f2d9b?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Layering đúng cách giúp hương thơm độc bản hơn mà vẫn giữ được sự hài hòa tổng thể.</p><p>Hãy bắt đầu từ body lotion trung tính, sau đó xịt lớp nền sạch và kết thúc bằng note điểm nhấn.</p>"
            },
            new
            {
                Title = "Perfume care: bảo quản nước hoa đúng chuẩn",
                Slug = "perfume-care-bao-quan-nuoc-hoa-dung-chuan",
                ThumbnailUrl = "https://images.unsplash.com/photo-1523293182086-7651a899d37f?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Tránh ánh nắng trực tiếp, nhiệt độ cao và thay đổi nhiệt liên tục để giữ chất lượng tinh dầu lâu dài.</p><p>Ưu tiên lưu trữ trong tủ kín, khô ráo, không để trong phòng tắm.</p>"
            },
            new
            {
                Title = "Mẹo chọn quà nước hoa cao cấp không bị sai",
                Slug = "meo-chon-qua-nuoc-hoa-cao-cap-khong-bi-sai",
                ThumbnailUrl = "https://images.unsplash.com/photo-1592945403244-b3fbafd7f539?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Chọn quà theo ngữ cảnh sử dụng và nhóm hương an toàn để tăng tỉ lệ phù hợp.</p><p>Set quà gồm mini size và bodycare cùng tông hương là phương án ít rủi ro.</p>"
            },
            new
            {
                Title = "So sánh Parfum, EDP, EDT dễ hiểu nhất",
                Slug = "so-sanh-parfum-edp-edt-de-hieu-nhat",
                ThumbnailUrl = "https://images.unsplash.com/photo-1588405748880-12d1d2a59b1e?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Sự khác biệt chính nằm ở nồng độ tinh dầu, thời gian lưu hương và cách mùi hương phát triển theo thời gian.</p><p>Không có loại nào tốt tuyệt đối, chỉ có lựa chọn phù hợp mục đích sử dụng.</p>"
            },
            new
            {
                Title = "Top mùi hương hẹn hò buổi tối sang trọng",
                Slug = "top-mui-huong-hen-ho-buoi-toi-sang-trong",
                ThumbnailUrl = "https://images.unsplash.com/photo-1590736969955-71cc94901144?auto=format&fit=crop&w=1200&q=80",
                Content = "<p>Buổi tối phù hợp hơn với các tone ấm như vanilla, amber, gỗ hoặc da thuộc tinh tế.</p><p>Hãy tiết chế số lần xịt để tổng thể cuốn hút nhưng vẫn lịch sự ở cự ly gần.</p>"
            }
        };

        for (var i = 0; i < newsSeed.Length; i++)
        {
            var item = newsSeed[i];
            var existing = await context.News.FirstOrDefaultAsync(x => x.Slug == item.Slug || x.Title == item.Title);

            if (existing == null)
            {
                context.News.Add(new News
                {
                    Title = item.Title,
                    Content = item.Content,
                    Image = item.ThumbnailUrl,
                    ThumbnailUrl = item.ThumbnailUrl,
                    Slug = item.Slug,
                    NewsStatus = "Published",
                    Status = true,
                    CreatedAt = now.AddDays(-(i + 1))
                });
                continue;
            }

            existing.Title = item.Title;
            existing.Content = item.Content;
            existing.Image = item.ThumbnailUrl;
            existing.ThumbnailUrl = item.ThumbnailUrl;
            existing.Slug = item.Slug;
            existing.NewsStatus = "Published";
            existing.Status = true;
            existing.CreatedAt = now.AddDays(-(i + 1));
        }

        await context.SaveChangesAsync();
    }

    private static string BuildDefaultDetailSpecsJson(int i, string brandName, string gender, string concentration)
    {
        var origin = i % 3 == 0 ? "Ý" : (i % 3 == 1 ? "Pháp" : "Anh");
        var year = 2018 + (i % 6);
        var perfumer = i % 2 == 0 ? "Sophie Labbe" : "Dominique Ropion";
        var genderLabel = gender == "Nam" ? "Nước hoa nam" : (gender == "Nữ" ? "Nước hoa nữ" : "Unisex");
        var concLabel = concentration switch
        {
            "EDP" => "Eau De Parfum",
            "EDT" => "Eau De Toilette",
            "EDC" => "Eau De Cologne",
            "Parfum" => "Parfum",
            _ => concentration
        };
        var rows = new[]
        {
            new { label = "Xuất xứ", value = origin },
            new { label = "Thương hiệu", value = $"Nước hoa {brandName}" },
            new { label = "Năm phát hành", value = year.ToString() },
            new { label = "Nhà pha chế", value = perfumer },
            new { label = "Giới tính", value = genderLabel },
            new { label = "Phong cách", value = "Thanh lịch, Thu hút" },
            new { label = "Nhóm hương", value = "Hương hoa cổ phương Đông" },
            new { label = "Nồng độ", value = concLabel },
            new { label = "Thời gian lưu hương", value = "3-6h" },
            new { label = "Độ tỏa hương", value = "Tỏa hương trong khoảng 1m" },
            new { label = "Hương đầu", value = "Cam bergamot, Bạc hà" },
            new { label = "Hương giữa", value = "Hoa hồng, Hoa nhài" },
            new { label = "Hương cuối", value = "Gỗ đàn hương, Hổ phách" }
        };
        return JsonSerializer.Serialize(rows);
    }

    private static async Task SeedProductsAsync(VanAnhPerfumeContext context)
    {
        if (await context.Products.CountAsync() >= 50 && await context.ProductVariants.CountAsync() >= 150)
        {
            return;
        }

        var categories = await context.Categories.AsNoTracking().ToListAsync();
        var brands = await context.Brands.AsNoTracking().ToListAsync();

        if (categories.Count == 0 || brands.Count == 0)
        {
            return;
        }

        var genders = new[] { "Nam", "Nữ", "Unisex" };
        var concentrations = new[] { "Parfum", "EDP", "EDT", "EDC" };
        var sizes = new[] { "10ml", "50ml", "100ml" };
        var imagePool = new[]
        {
            "https://picsum.photos/id/100/900/900",
            "https://picsum.photos/id/101/900/900",
            "https://picsum.photos/id/102/900/900",
            "https://picsum.photos/id/103/900/900",
            "https://picsum.photos/id/104/900/900",
            "https://picsum.photos/id/105/900/900",
            "https://picsum.photos/id/106/900/900",
            "https://picsum.photos/id/107/900/900",
            "https://picsum.photos/id/108/900/900",
            "https://picsum.photos/id/109/900/900",
            "https://picsum.photos/id/110/900/900",
            "https://picsum.photos/id/111/900/900"
        };

        var existingByName = await context.Products.ToDictionaryAsync(x => x.Name, x => x.ProductId);
        var skuSet = (await context.ProductVariants.Select(x => x.Sku).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nameTokens = new[] { "Noir", "Blanc", "Intense", "Signature", "Velvet", "Mystic", "Amber", "Ocean", "Bloom", "Reserve" };

        var targetProducts = 50;
        for (var i = 1; i <= targetProducts; i++)
        {
            var productName = $"Luxury Perfume {nameTokens[i % nameTokens.Length]} {i:00}";
            var brandEntity = brands[i % brands.Count];
            var genderVal = genders[i % genders.Length];
            var concVal = concentrations[i % concentrations.Length];
            var shortPlain = $"Mùi hương #{i:00} cân bằng độ bám tỏa và tính ứng dụng hàng ngày.";
            var longHtml =
                $"<p>Dòng hương cao cấp #{i:00}, phù hợp cả đi làm và đi tiệc tối.</p>" +
                "<p>Thiết kế cân bằng giữa độ lưu hương và sự thanh thoát, phù hợp nhiều phong cách.</p>";
            var specsJson = BuildDefaultDetailSpecsJson(i, brandEntity.Name, genderVal, concVal);
            Product product;

            if (existingByName.TryGetValue(productName, out var existingId))
            {
                product = await context.Products.FirstAsync(x => x.ProductId == existingId);
                product.BrandId = brandEntity.BrandId;
                product.CategoryId = categories[i % categories.Count].CategoryId;
                product.Gender = genderVal;
                product.Concentration = concVal;
                product.Description = longHtml;
                product.ShortDescription = shortPlain;
                product.DetailSpecsJson = specsJson;
                product.MainImage = imagePool[i % imagePool.Length];
                product.IsFeatured = i % 2 == 0;
                product.Status = true;
                product.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                product = new Product
                {
                    Name = productName,
                    BrandId = brandEntity.BrandId,
                    CategoryId = categories[i % categories.Count].CategoryId,
                    Gender = genderVal,
                    Concentration = concVal,
                    Description = longHtml,
                    ShortDescription = shortPlain,
                    DetailSpecsJson = specsJson,
                    MainImage = imagePool[i % imagePool.Length],
                    IsFeatured = i % 2 == 0,
                    Status = true
                };
                context.Products.Add(product);
                await context.SaveChangesAsync();
                existingByName[productName] = product.ProductId;
            }

            product.ProductStatus = "Active";
            product.Slug = ToSlug(productName);
            product.MetaTitle = $"{productName} | Vân Anh Perfume";
            product.MetaDescription = $"Sản phẩm {productName} với nhiều phiên bản dung tích và note mùi.";
            product.RelatedProductIds = null;

            var existingImages = await context.ProductImages
                .Where(x => x.ProductId == product.ProductId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            if (existingImages.Count == 0)
            {
                context.ProductImages.AddRange(
                    new ProductImage { ProductId = product.ProductId, ImageUrl = imagePool[i % imagePool.Length], IsPrimary = true, SortOrder = 1 },
                    new ProductImage { ProductId = product.ProductId, ImageUrl = imagePool[(i + 1) % imagePool.Length], IsPrimary = false, SortOrder = 2 },
                    new ProductImage { ProductId = product.ProductId, ImageUrl = imagePool[(i + 2) % imagePool.Length], IsPrimary = false, SortOrder = 3 }
                );
            }
            else if (existingImages.Count < 3)
            {
                for (var imgIdx = existingImages.Count; imgIdx < 3; imgIdx++)
                {
                    context.ProductImages.Add(new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImageUrl = imagePool[(i + imgIdx) % imagePool.Length],
                        IsPrimary = false,
                        SortOrder = imgIdx + 1
                    });
                }
            }

            for (var v = 0; v < sizes.Length; v++)
            {
                var sku = $"TEST-{i:000}-{sizes[v]}";
                if (skuSet.Contains(sku))
                {
                    continue;
                }

                context.ProductVariants.Add(new ProductVariant
                {
                    ProductId = product.ProductId,
                    Size = sizes[v],
                    Price = 900000 + (i * 55000) + (v * 120000),
                    Stock = 20 + i + v,
                    Sku = sku,
                    Color = v == 0 ? "Black" : (v == 1 ? "Gold" : "Silver"),
                    Material = "Glass",
                    VariantImageUrl = imagePool[(i + v) % imagePool.Length]
                });
                skuSet.Add(sku);
            }
        }

        await context.SaveChangesAsync();

        if (await context.Reviews.CountAsync() < 120)
        {
            var customerIds = await context.Users
                .Where(x => x.Role.RoleName == "Customer")
                .Select(x => x.UserId)
                .ToListAsync();
            var productIds = await context.Products.Select(x => x.ProductId).Take(40).ToListAsync();

            var reviewTarget = Math.Min(customerIds.Count * 4, 120);
            for (var i = 0; i < reviewTarget; i++)
            {
                var productId = productIds[i % productIds.Count];
                var userId = customerIds[i % customerIds.Count];
                if (await context.Reviews.AnyAsync(x => x.ProductId == productId && x.UserId == userId))
                {
                    continue;
                }

                context.Reviews.Add(new Review
                {
                    ProductId = productId,
                    UserId = userId,
                    Rating = 3 + (i % 3),
                    ReviewerName = $"Khach Hang {userId:00}",
                    ReviewerEmail = $"customer{userId:00}@vananh.test",
                    Title = $"Đánh giá sản phẩm #{productId}",
                    Content = $"Mùi hương cân bằng, độ lưu hương ổn trong khoảng {5 + (i % 5)} giờ.",
                    Status = i % 5 == 0 ? "Pending" : "Approved",
                    IsVerifiedPurchase = i % 2 == 0,
                    Comment = $"Danh gia test #{i + 1}: mui huong ben, toa huong tot.",
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    UpdatedAt = DateTime.UtcNow.AddDays(-i + 1)
                });
            }

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedPromotionsAsync(VanAnhPerfumeContext context, DateTime now)
    {
        var promotions = new[]
        {
            new Promotion
            {
                Name = "AUTO10",
                DiscountPercent = 10,
                StartDate = now.AddDays(-30),
                EndDate = now.AddYears(1),
                MinOrderValue = 500000,
                ApplicableCategoryIds = null,
                ApplicableProductIds = null,
                AutoApply = true,
                IsActive = true
            },
            new Promotion
            {
                Name = "VIP15",
                DiscountPercent = 15,
                StartDate = now.AddDays(-15),
                EndDate = now.AddMonths(6),
                MinOrderValue = 1500000,
                ApplicableCategoryIds = null,
                ApplicableProductIds = null,
                AutoApply = false,
                IsActive = true
            }
        };

        foreach (var promotion in promotions)
        {
            var existing = await context.Promotions.FirstOrDefaultAsync(x => x.Name == promotion.Name);
            if (existing == null)
            {
                context.Promotions.Add(promotion);
                continue;
            }
            existing.DiscountPercent = promotion.DiscountPercent;
            existing.StartDate = promotion.StartDate;
            existing.EndDate = promotion.EndDate;
            existing.MinOrderValue = promotion.MinOrderValue;
            existing.ApplicableCategoryIds = promotion.ApplicableCategoryIds;
            existing.ApplicableProductIds = promotion.ApplicableProductIds;
            existing.AutoApply = promotion.AutoApply;
            existing.IsActive = promotion.IsActive;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedCouponsLegacyAsync(VanAnhPerfumeContext context, DateTime now)
    {
        var legacyCoupons = await context.Coupons
            .Where(x => EF.Functions.Like(x.Code, "TEST-%")
                     || EF.Functions.Like(x.Code, "LEGACY-%"))
            .ToListAsync();
        foreach (var coupon in legacyCoupons)
        {
            coupon.IsActive = false;
            coupon.AutoApply = false;
            coupon.StartDate ??= now.AddYears(-1);
            if (coupon.ExpiryDate < now)
            {
                continue;
            }
            coupon.ExpiryDate = now.AddDays(-1);
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsureProductHoverImagesAsync(VanAnhPerfumeContext context)
    {
        var fallbackImages = new[]
        {
            "https://images.unsplash.com/photo-1594035910387-fea47794261f?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1592945403244-b3fbafd7f539?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1615634262417-44b37c6f2d9b?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1563170351-be82bc888aa4?auto=format&fit=crop&w=900&q=80"
        };

        var products = await context.Products
            .Include(x => x.ProductImages)
            .OrderBy(x => x.ProductId)
            .ToListAsync();

        foreach (var product in products)
        {
            var images = product.ProductImages
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.SortOrder)
                .ToList();

            if (images.Count == 0)
            {
                var primaryUrl = !string.IsNullOrWhiteSpace(product.MainImage)
                    ? product.MainImage!
                    : fallbackImages[product.ProductId % fallbackImages.Length];
                var hoverUrl = fallbackImages[(product.ProductId + 1) % fallbackImages.Length];

                context.ProductImages.AddRange(
                    new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImageUrl = primaryUrl,
                        IsPrimary = true,
                        SortOrder = 1
                    },
                    new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImageUrl = hoverUrl,
                        IsPrimary = false,
                        SortOrder = 2
                    });

                product.MainImage = primaryUrl;
                continue;
            }

            var primary = images.First();
            if (!images.Any(x => x.IsPrimary))
            {
                primary.IsPrimary = true;
            }

            if (string.IsNullOrWhiteSpace(product.MainImage))
            {
                product.MainImage = primary.ImageUrl;
            }

            for (var i = 0; i < images.Count; i++)
            {
                images[i].SortOrder = i + 1;
            }

            if (images.Count == 1)
            {
                var cloneUrl = !string.IsNullOrWhiteSpace(images[0].ImageUrl)
                    ? images[0].ImageUrl
                    : fallbackImages[product.ProductId % fallbackImages.Length];
                context.ProductImages.Add(new ProductImage
                {
                    ProductId = product.ProductId,
                    ImageUrl = cloneUrl,
                    IsPrimary = false,
                    SortOrder = 2
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedNewsletterAsync(VanAnhPerfumeContext context)
    {
        for (var i = 1; i <= 20; i++)
        {
            var email = $"subscriber{i:00}@vananh.test";
            if (await context.NewsletterSubscribers.AnyAsync(x => x.Email == email))
            {
                continue;
            }

            context.NewsletterSubscribers.Add(new NewsletterSubscriber
            {
                Email = email,
                SubscribedAt = DateTime.UtcNow.AddDays(-i),
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedBannersAsync(VanAnhPerfumeContext context, DateTime now)
    {
        var banners = new[]
        {
            new Banner
            {
                Title = "Gucci Bloom",
                SubTitle = "Bo suu tap moi",
                ImageUrl = "https://images.unsplash.com/photo-1523293182086-7651a899d37f?auto=format&fit=crop&w=1600&q=80",
                LinkUrl = "/Product",
                SortOrder = 1,
                IsActive = true,
                DisplayFrom = now.AddDays(-1),
                DisplayTo = now.AddYears(2)
            },
            new Banner
            {
                Title = "Dior Sauvage",
                SubTitle = "Huong thom danh cho quy ong",
                ImageUrl = "https://images.unsplash.com/photo-1612817288484-6f916006741a?auto=format&fit=crop&w=1600&q=80",
                LinkUrl = "/Product?gender=Nam",
                SortOrder = 2,
                IsActive = true,
                DisplayFrom = now.AddDays(-1),
                DisplayTo = now.AddYears(2)
            },
            new Banner
            {
                Title = "Unisex Collection",
                SubTitle = "Tinh te, toi gian, sang trong",
                ImageUrl = "https://images.unsplash.com/photo-1590736969955-71cc94901144?auto=format&fit=crop&w=1600&q=80",
                LinkUrl = "/Product?gender=Unisex",
                SortOrder = 3,
                IsActive = true,
                DisplayFrom = now.AddDays(-1),
                DisplayTo = now.AddYears(2)
            }
        };

        foreach (var banner in banners)
        {
            var existing = await context.Banners.FirstOrDefaultAsync(x => x.Title == banner.Title);
            if (existing == null)
            {
                context.Banners.Add(banner);
                continue;
            }
            existing.SubTitle = banner.SubTitle;
            existing.ImageUrl = banner.ImageUrl;
            existing.LinkUrl = banner.LinkUrl;
            existing.SortOrder = banner.SortOrder;
            existing.IsActive = banner.IsActive;
            existing.DisplayFrom = banner.DisplayFrom;
            existing.DisplayTo = banner.DisplayTo;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedOrdersAsync(VanAnhPerfumeContext context, DateTime now)
    {
        if (await context.Orders.CountAsync() >= 80)
        {
            return;
        }

        var customers = await context.Users
            .Where(x => x.Role.RoleName == "Customer")
            .OrderBy(x => x.UserId)
            .Take(20)
            .ToListAsync();
        var variants = await context.ProductVariants
            .Include(x => x.Product)
            .OrderBy(x => x.VariantId)
            .Take(120)
            .ToListAsync();

        if (customers.Count == 0 || variants.Count < 3)
        {
            return;
        }

        var orderTarget = 80;
        for (var i = 1; i <= orderTarget; i++)
        {
            var customer = customers[i % customers.Count];
            var orderDate = now.AddHours(-(i * 9));
            var orderCode = $"ORD-{orderDate:yyyyMMdd}-{customer.UserId:000}-{i:00000}";
            if (await context.Orders.AnyAsync(x => x.OrderCode == orderCode))
            {
                continue;
            }

            var line1 = variants[i % variants.Count];
            var line2 = variants[(i + 1) % variants.Count];
            var quantity1 = 1 + (i % 2);
            var quantity2 = 1;
            var total = (line1.Price * quantity1) + (line2.Price * quantity2);
            var paid = i % 4 != 0;
            var status = paid ? (i % 3 == 0 ? "Completed" : "Processing") : (i % 5 == 0 ? "Cancelled" : "Pending");
            var paymentMethod = i % 2 == 0 ? "VNPAY" : "COD";
            var shippingStatus = status is "Completed" ? "Delivered" : (status == "Processing" ? "Processing" : "Pending");
            var discount = total * 0.1m;

            var order = new Order
            {
                UserId = customer.UserId,
                OrderDate = orderDate,
                TotalAmount = total,
                Status = status,
                OrderCode = orderCode,
                FullName = customer.FullName,
                CustomerEmail = customer.Email,
                Phone = customer.Phone ?? "0900000000",
                Address = "123 Test Street, Ho Chi Minh City",
                PaymentStatus = paid ? "Paid" : "Pending",
                ShippingStatus = shippingStatus,
                TrackingCode = paid ? $"TRACK-{1000 + i}" : null,
                RefundStatus = null,
                AdminNote = $"Promotion#AUTO10 -{discount:N0}",
                SubTotal = total,
                DiscountAmount = discount,
                ShippingFee = 0,
                CouponCode = "AUTO10",
                UpdatedAt = orderDate
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            context.OrderDetails.AddRange(
                new OrderDetail { OrderId = order.OrderId, VariantId = line1.VariantId, ProductName = line1.Product.Name, VariantName = line1.Size, Sku = line1.Sku, Quantity = quantity1, Price = line1.Price },
                new OrderDetail { OrderId = order.OrderId, VariantId = line2.VariantId, ProductName = line2.Product.Name, VariantName = line2.Size, Sku = line2.Sku, Quantity = quantity2, Price = line2.Price }
            );

            context.Payments.Add(new Payment
            {
                OrderId = order.OrderId,
                Amount = total - discount,
                PaymentMethod = paymentMethod,
                Status = paid ? "Paid" : "Pending",
                TransactionId = paid ? $"TXN-{orderCode}" : null,
                CreatedAt = orderDate,
                PaidAt = paid ? orderDate.AddMinutes(15) : null
            });

            context.OrderStatusLogs.Add(new OrderStatusLog
            {
                OrderId = order.OrderId,
                OldStatus = "Pending",
                NewStatus = status,
                OldPaymentStatus = "Pending",
                NewPaymentStatus = paid ? "Paid" : "Pending",
                Note = $"Seeded order using {paymentMethod}",
                ChangedBy = "Seeder",
                CreatedAt = orderDate.AddMinutes(2)
            });
        }

        await context.SaveChangesAsync();
    }

    private static string ToSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized
            .Replace("đ", "d")
            .Replace("á", "a").Replace("à", "a").Replace("ả", "a").Replace("ã", "a").Replace("ạ", "a")
            .Replace("ă", "a").Replace("ắ", "a").Replace("ằ", "a").Replace("ẳ", "a").Replace("ẵ", "a").Replace("ặ", "a")
            .Replace("â", "a").Replace("ấ", "a").Replace("ầ", "a").Replace("ẩ", "a").Replace("ẫ", "a").Replace("ậ", "a")
            .Replace("é", "e").Replace("è", "e").Replace("ẻ", "e").Replace("ẽ", "e").Replace("ẹ", "e")
            .Replace("ê", "e").Replace("ế", "e").Replace("ề", "e").Replace("ể", "e").Replace("ễ", "e").Replace("ệ", "e")
            .Replace("í", "i").Replace("ì", "i").Replace("ỉ", "i").Replace("ĩ", "i").Replace("ị", "i")
            .Replace("ó", "o").Replace("ò", "o").Replace("ỏ", "o").Replace("õ", "o").Replace("ọ", "o")
            .Replace("ô", "o").Replace("ố", "o").Replace("ồ", "o").Replace("ổ", "o").Replace("ỗ", "o").Replace("ộ", "o")
            .Replace("ơ", "o").Replace("ớ", "o").Replace("ờ", "o").Replace("ở", "o").Replace("ỡ", "o").Replace("ợ", "o")
            .Replace("ú", "u").Replace("ù", "u").Replace("ủ", "u").Replace("ũ", "u").Replace("ụ", "u")
            .Replace("ư", "u").Replace("ứ", "u").Replace("ừ", "u").Replace("ử", "u").Replace("ữ", "u").Replace("ự", "u")
            .Replace("ý", "y").Replace("ỳ", "y").Replace("ỷ", "y").Replace("ỹ", "y").Replace("ỵ", "y");

        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "-");
        normalized = Regex.Replace(normalized, @"-+", "-").Trim('-');
        return normalized;
    }
}
