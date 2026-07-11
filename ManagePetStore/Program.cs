using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ManagePetStore.Models;
using ManagePetStore.Repositories;
using ManagePetStore.Services;
using PayOS;


var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. CẤU HÌNH CƠ SỞ DỮ LIỆU (ENTITY FRAMEWORK CORE)
// =========================================================================
var configuredConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
var sqlConnection = new SqlConnectionStringBuilder(configuredConnection)
{
    ConnectTimeout = 5,
    ConnectRetryCount = 0
};

builder.Services.AddDbContext<PetStoreManagementContext>(options =>
    options.UseSqlServer(sqlConnection.ConnectionString, sqlOptions => sqlOptions.CommandTimeout(15)));

// =========================================================================
// 2. CẤU HÌNH XÁC THỰC & PHÂN QUYỀN (COOKIE AUTHENTICATION)
// =========================================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Đường dẫn đến trang đăng nhập nếu người dùng chưa xác thực
        options.LoginPath = "/Customer/Account/Login";

        // Đường dẫn đến trang thông báo từ chối truy cập nếu sai Quyền (Role)
        options.AccessDeniedPath = "/Customer/Account/AccessDenied";

        // Thời gian duy trì đăng nhập (7 ngày)
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                var path = context.Request.Path.Value ?? "";
                if (path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/Warehouse", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/Manager", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/SpaServices", StringComparison.OrdinalIgnoreCase))
                {
                    context.RedirectUri = "/Staff/Login";
                }
                else
                {
                    context.RedirectUri = "/Customer/Account/Login";
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

// =========================================================================
// 3. CẤU HÌNH LƯU TRỮ TẠM THỜI (SESSION - PHỤC VỤ GIỎ HÀNG GUEST)
// =========================================================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Giỏ hàng tồn tại 30 phút nếu không thao tác
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Thêm các dịch vụ hỗ trợ cho kiến trúc MVC
builder.Services.Configure<ManagePetStore.Services.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<ManagePetStore.Services.IEmailService, ManagePetStore.Services.EmailService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ManagePetStore.Services.Customer.CartProductResolver>();
builder.Services.AddScoped<ManagePetStore.Services.Customer.ICartService, ManagePetStore.Services.Customer.CartService>();
builder.Services.AddScoped<ManagePetStore.Services.Customer.IOrderReviewService, ManagePetStore.Services.Customer.OrderReviewService>();
builder.Services.AddScoped<ManagePetStore.Services.Customer.ICheckoutEmailService, ManagePetStore.Services.Customer.CheckoutEmailService>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new PayOSClient(
        config["PayOS:ClientId"] ?? "",
        config["PayOS:ApiKey"] ?? "",
        config["PayOS:ChecksumKey"] ?? ""
    );
});
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();


// =========================================================================
// 4. ĐĂNG KÝ DEPENDENCY INJECTION
// =========================================================================
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();

// Warehouse repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
builder.Services.AddScoped<ManagePetStore.Repositories.Warehouse.IInventoryBatchRepository, ManagePetStore.Repositories.Warehouse.InventoryBatchRepository>();
builder.Services.AddScoped<ManagePetStore.Repositories.Warehouse.IStockMovementRepository, ManagePetStore.Repositories.Warehouse.StockMovementRepository>();

// Warehouse services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductCategoryService, ProductCategoryService>();
builder.Services.AddScoped<IHotelBookingHistoryService, HotelBookingHistoryService>();
builder.Services.AddScoped<IHotelCareMediaService, HotelCareMediaService>();
builder.Services.AddScoped<ManagePetStore.Services.Warehouse.IInventoryBatchService, ManagePetStore.Services.Warehouse.InventoryBatchService>();
builder.Services.AddScoped<ManagePetStore.Services.Warehouse.IStockMovementService, ManagePetStore.Services.Warehouse.StockMovementService>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();

// Hosted Services
builder.Services.AddHostedService<ManagePetStore.Services.Hosted.ExpiryDateScannerService>();




var app = builder.Build();

// =========================================================================
// 4. CẤU HÌNH ĐƯỜNG ỐNG XỬ LÝ (MIDDLEWARE PIPELINE) - THỨ TỰ BẮT BUỘC
// =========================================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Cho phép truy cập các file tĩnh trong wwwroot (CSS, JS, Images, File upload của chuồng/pet)
app.UseStaticFiles();

// Phục vụ CSS/JS homepage từ thư mục Views (chỉ cho phép .css và .js)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/views-assets"))
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }

    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "Views")),
    RequestPath = "/views-assets"
});

app.UseRouting();

// Kích hoạt Session (Phải đặt trước Authentication và Authorization)
app.UseSession();

// Kích hoạt nhận diện danh tính (Ai đang đăng nhập?)
app.UseAuthentication();

// Kích hoạt kiểm tra quyền hạn (Tài khoản đó thuộc Role nào, được vào đâu?)
app.UseAuthorization();

// Đăng ký map hub cho SignalR
app.MapHub<ManagePetStore.Hubs.ChatHub>("/chatHub");
app.MapHub<ManagePetStore.Hubs.HotelCareHub>("/hotelCareHub");


// =========================================================================
// 5. CẤU HÌNH ĐỊNH TUYẾN (ROUTING) - CHIA LÃNH ĐỊA CHO CÁC THÀNH VIÊN
// =========================================================================

// Route ưu tiên 1: Dành cho các phân hệ nằm trong Areas (Admin, Customer, v.v.)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Route ưu tiên 2: Dành cho trang chủ công khai bên ngoài nếu không thuộc Area nào
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// =========================================================================
// TỰ ĐỘNG CẬP NHẬT DATABASE SCHEMA (CHO CẢ NHÓM)
// =========================================================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PetStoreManagementContext>();
    try
    {
        using var schemaUpdateCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        // Đồng bộ các phần schema bổ sung theo cách idempotent cho database hiện tại.
        await context.Database.ExecuteSqlRawAsync("""
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

            IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckInDate') IS NULL
                ALTER TABLE dbo.HotelBookings ADD ScheduledCheckInDate DATETIME NULL;

            IF COL_LENGTH(N'dbo.HotelBookings', N'ScheduledCheckOutDate') IS NULL
                ALTER TABLE dbo.HotelBookings ADD ScheduledCheckOutDate DATETIME NULL;

            IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckInAt') IS NULL
                ALTER TABLE dbo.HotelBookings ADD ActualCheckInAt DATETIME NULL;

            IF COL_LENGTH(N'dbo.HotelBookings', N'ActualCheckOutAt') IS NULL
                ALTER TABLE dbo.HotelBookings ADD ActualCheckOutAt DATETIME NULL;

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
                    FOREIGN KEY (HotelBookingId) REFERENCES dbo.HotelBookings(HotelBookingId) ON DELETE NO ACTION;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PetBioTimelines_HotelBookingId' AND object_id = OBJECT_ID(N'dbo.PetBioTimelines'))
                CREATE INDEX IX_PetBioTimelines_HotelBookingId ON dbo.PetBioTimelines(HotelBookingId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MedicalRecords_HotelBookingId' AND object_id = OBJECT_ID(N'dbo.MedicalRecords'))
                CREATE INDEX IX_MedicalRecords_HotelBookingId ON dbo.MedicalRecords(HotelBookingId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FoodDiaryLogs_HotelBookingId' AND object_id = OBJECT_ID(N'dbo.FoodDiaryLogs'))
                CREATE INDEX IX_FoodDiaryLogs_HotelBookingId ON dbo.FoodDiaryLogs(HotelBookingId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FoodDiaryLogs_Booking_OccurredAt' AND object_id = OBJECT_ID(N'dbo.FoodDiaryLogs'))
                CREATE INDEX IX_FoodDiaryLogs_Booking_OccurredAt ON dbo.FoodDiaryLogs(HotelBookingId, OccurredAt DESC);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerNotifications_Customer_Unread_CreatedAt' AND object_id = OBJECT_ID(N'dbo.CustomerNotifications'))
                CREATE INDEX IX_CustomerNotifications_Customer_Unread_CreatedAt
                    ON dbo.CustomerNotifications(CustomerId, IsRead, CreatedAt DESC);

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
            """, schemaUpdateCancellation.Token);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB Update Error] Không thể tự động cập nhật Database: {ex.Message}");
    }
}

app.Run();
