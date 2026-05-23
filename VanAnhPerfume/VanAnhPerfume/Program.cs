using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Filters;
using VanAnhPerfume.Models.Options;
using VanAnhPerfume.Repositories;
using VanAnhPerfume.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================================
// 1. ĐĂNG KÝ DỊCH VỤ (SERVICES) - TRƯỚC KHI .Build()
// ==========================================================

builder.Services.AddScoped<AdminAreaRoleSyncFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AdminAreaRoleSyncFilter>();
});

// --- Kết nối Database ---
builder.Services.AddDbContext<VanAnhPerfumeContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(3)));

// --- Đăng ký Repository & Unit of Work (Dependency Injection) ---
// Giúp Controller có thể gọi được dữ liệu thông qua Interface
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// --- Cấu hình Session (Dùng cho Giỏ hàng) ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// --- Cấu hình Cookie Authentication (Bảo mật Đăng nhập) ---
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options => {
        options.Cookie.Name = "VanAnhPerfume.Auth";
        options.LoginPath = "/Account/Login"; // Đường dẫn nếu chưa đăng nhập mà đòi vào trang cấm
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7); // Ghi nhớ đăng nhập trong 7 ngày
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderConfirmationNotifier, OrderConfirmationNotifier>();
builder.Services.AddScoped<IVnpayService, VnpayService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.Configure<VnpayOptions>(builder.Configuration.GetSection("Vnpay"));

var app = builder.Build();

// f
var provider = new FileExtensionContentTypeProvider();

provider.Mappings[".avif"] = "image/avif";
provider.Mappings[".webp"] = "image/webp";

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VanAnhPerfumeContext>();

    var enableSeedOnStartup = builder.Configuration.GetValue<bool>("Seed:EnableOnStartup");

    if (enableSeedOnStartup)
    {
        var resetSeedData = builder.Configuration.GetValue<bool>("Seed:ResetOnStartup");
        await AppDataSeeder.SeedAsync(db, resetSeedData);
    }
}

// ==========================================================
// 2. CẤU HÌNH ĐƯỜNG ĐI (MIDDLEWARE PIPELINE) - SAU KHI .Build()
// ==========================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

app.UseRouting();

// --- QUAN TRỌNG: Thứ tự Middleware phải chuẩn xác ---
app.UseSession();        // 1. Bật Session trước
app.UseAuthentication(); // 2. Kiểm tra danh tính (Ai?)
app.UseAuthorization();  // 3. Kiểm tra quyền hạn (Được làm gì?)

// --- Cấu hình Route (Đường dẫn) ---

// Route dành cho Admin (Area)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Route mặc định cho Khách hàng
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();