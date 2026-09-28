using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewsWebApp.Data;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Implementations;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. Database & EF Core Configuration
// ==========================================
var provider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// ==========================================
// 2. Identity Configuration
// ==========================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // User policy settings
    options.User.RequireUniqueEmail = true;

    // SignIn settings
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure application authentication cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// ==========================================
// 3. Dependency Injection (Repository Pattern & Services)
// ==========================================
builder.Services.AddScoped<INewsRepository, NewsRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IAuthorRepository, AuthorRepository>();
builder.Services.AddScoped<IBookmarkRepository, BookmarkRepository>();
builder.Services.AddScoped<INewsletterRepository, NewsletterRepository>();
builder.Services.AddScoped<INewsFeedSourceRepository, NewsFeedSourceRepository>();

// Live News Ingestion Engine & Background Poller
builder.Services.AddHttpClient("LiveNewsClient");
builder.Services.AddScoped<ILiveNewsService, LiveNewsService>();
builder.Services.AddHostedService<LiveNewsSyncBackgroundService>();

// ==========================================
// 4. MVC & Routing Configuration
// ==========================================
builder.Services.AddControllersWithViews();

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

var app = builder.Build();

// ==========================================
// 5. Automatic Database Seeding on Startup
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        logger.LogInformation("Checking database initialization and seeding...");
        await DbInitializer.InitializeAsync(context, userManager, roleManager);
        logger.LogInformation("Database initialization completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database initialization warning: {Message}", ex.Message);
    }
}

// ==========================================
// 6. Middleware Pipeline Configuration
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Custom SEO-friendly routing for slugs
app.MapControllerRoute(
    name: "newsCategory",
    pattern: "news/category/{slug}",
    defaults: new { controller = "News", action = "Category" });

app.MapControllerRoute(
    name: "newsDetails",
    pattern: "news/details/{slug}",
    defaults: new { controller = "News", action = "Details" });

// Standard default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
