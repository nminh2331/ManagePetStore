using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ManagePetStore.Models;
using ManagePetStore.Repositories;
using ManagePetStore.Services;


var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. C?U H�NH CO S? D? LI?U (ENTITY FRAMEWORK CORE)
// =========================================================================
builder.Services.AddDbContext<PetStoreManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =========================================================================
// 2. C?U H�NH X�C TH?C & PH�N QUY?N (COOKIE AUTHENTICATION)
// =========================================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // �u?ng d?n d?n trang dang nh?p n?u ngu?i d�ng chua x�c th?c
        options.LoginPath = "/CustomerAccount/Login";

        // �u?ng d?n d?n trang th�ng b�o t? ch?i truy c?p n?u sai Quy?n (Role)
        options.AccessDeniedPath = "/CustomerAccount/AccessDenied";

        // Th?i gian duy tr� dang nh?p (7 ng�y)
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
                    path.StartsWith("/ServiceStaff", StringComparison.OrdinalIgnoreCase))
                {
                    context.RedirectUri = "/Staff/Login";
                }
                else
                {
                    context.RedirectUri = "/CustomerAccount/Login";
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

// =========================================================================
// 3. C?U H�NH LUU TR? T?M TH?I (SESSION - PH?C V? GI? H�NG GUEST)
// =========================================================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Gi? h�ng t?n t?i 30 ph�t n?u kh�ng thao t�c
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Th�m c�c d?ch v? h? tr? cho ki?n tr�c MVC
builder.Services.Configure<ManagePetStore.Services.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<ManagePetStore.Services.IEmailService, ManagePetStore.Services.EmailService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ManagePetStore.Services.Customer.CartProductResolver>();
builder.Services.AddScoped<ManagePetStore.Services.Customer.ICartService, ManagePetStore.Services.Customer.CartService>();
builder.Services.AddScoped<ManagePetStore.Services.Customer.IOrderReviewService, ManagePetStore.Services.Customer.OrderReviewService>();
builder.Services.AddScoped<ManagePetStore.Services.Customer.ICheckoutEmailService, ManagePetStore.Services.Customer.CheckoutEmailService>();
builder.Services.AddControllersWithViews();

// =========================================================================
// 4. �ANG K� DEPENDENCY INJECTION
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

// Background Services
builder.Services.AddHostedService<ManagePetStore.BackgroundServices.ExpiryDateScannerService>();




var app = builder.Build();

// =========================================================================
// 4. C?U H�NH �U?NG ?NG X? L� (MIDDLEWARE PIPELINE) - TH? T? B?T BU?C
// =========================================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Cho ph�p truy c?p c�c file tinh trong wwwroot (CSS, JS, Images, File upload c?a chu?ng/pet)
app.UseStaticFiles();

// Ph?c v? CSS/JS homepage t? thu m?c Views (ch? cho ph�p .css v� .js)
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

// K�ch ho?t Session (Ph?i d?t tru?c Authentication v� Authorization)
app.UseSession();

// K�ch ho?t nh?n di?n danh t�nh (Ai dang dang nh?p?)
app.UseAuthentication();

// K�ch ho?t ki?m tra quy?n h?n (T�i kho?n d� thu?c Role n�o, du?c v�o d�u?)
app.UseAuthorization();

// =========================================================================
// 5. C?U H�NH �?NH TUY?N (ROUTING)
// =========================================================================

// Route m?c d?nh
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
