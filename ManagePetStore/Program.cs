using Microsoft.AspNetCore.Authentication.Cookies;
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
builder.Services.AddDbContext<PetStoreManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
                    path.StartsWith("/Cashier", StringComparison.OrdinalIgnoreCase) ||
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

app.Run();
