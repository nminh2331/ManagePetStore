using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
<<<<<<< HEAD
using ManagePetStore.Models; // CHÚ Ý: Đổi lại tên Namespace này nếu tên Project của mày đặt khác
=======
using ManagePetStore.Model;
using ManagePetStore.Repositories;
using ManagePetStore.Services;
>>>>>>> Nam

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
builder.Services.AddControllersWithViews();

// =========================================================================
// 4. ĐĂNG KÝ DEPENDENCY INJECTION
// =========================================================================
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();


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