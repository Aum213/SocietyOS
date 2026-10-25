using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SocietyOS.Data;
using SocietyOS.Models;
using SocietyOS.Services;

var builder = WebApplication.CreateBuilder(args);

// Connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configure DbContext with ApplicationUser
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure Identity with Role support
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Application Services
builder.Services.AddSingleton<IDailyMaximService, DailyMaximService>();

builder.Services.AddControllersWithViews();

// Place alongside existing service registrations in Program.cs:
builder.Services.AddScoped<IVisitorPhotoStorageService, VisitorPhotoStorageService>();
builder.Services.AddHostedService<VisitorRetentionWorker>();

builder.Services.AddSingleton<ISocietyTimeZoneService, SocietyTimeZoneService>();

builder.Services.AddScoped<IIssuePhotoStorageService, IssuePhotoStorageService>();

builder.Services.AddScoped<IMarketplacePhotoStorageService, MarketplacePhotoStorageService>();

builder.Services.AddSignalR();

var app = builder.Build();

// Run startup database seeder
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.SeedAsync(services, app.Configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the SocietyOS database.");
    }
}

// HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<SocietyOS.Hubs.EmergencySosHub>("/hubs/emergency-sos");

app.Run();