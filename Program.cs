using EmailChatASP.Services;

var builder = WebApplication.CreateBuilder(args);

// Thêm các dịch vụ cần thiết
builder.Services.AddControllersWithViews(); // MVC service
builder.Services.AddSingleton<EmailService>();  // Thêm EmailService

// --- Cấu hình HttpClientFactory và ChatService ---
builder.Services.AddHttpClient("OpenAIClient"); // Đăng ký một HttpClient có tên
builder.Services.AddScoped<ChatService>(); // Đăng ký ChatService (Scoped phù hợp cho dịch vụ dùng 
var app = builder.Build();

// Cấu hình pipeline HTTP request
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Định nghĩa route mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Định nghĩa route cho Chat
app.MapControllerRoute(
    name: "chat",
    pattern: "chat/{action=Index}/{id?}");

// Chạy ứng dụng
app.Run();
