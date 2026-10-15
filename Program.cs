using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartParkingMS.Data;
using SmartParkingMS.Models;
using SmartParkingMS.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----

// Database (EF Core + SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ASP.NET Core Identity (handles registration, login, roles - FR-01, FR-02, FR-03)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// MVC (Controllers + Views) - required architecture per requirement analysis
builder.Services.AddControllersWithViews();
builder.Services.Configure<SslCommerzOptions>(builder.Configuration.GetSection("SSLCOMMERZ"));
builder.Services.AddHttpClient<SslCommerzService>();

var app = builder.Build();

// ---- Seed database (roles, admin account, sample slots) ----
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(services);
}

// ---- HTTP request pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // must precede Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
