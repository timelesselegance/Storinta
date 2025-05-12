using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using HumanBodyWeb.Data;
using HumanBodyWeb.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql; // NpgsqlConnectionStringBuilder için eklendi

var builder = WebApplication.CreateBuilder(args);
var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole()); // Erken loglama için fabrika
var earlyLogger = loggerFactory.CreateLogger<Program>(); // Erken logger

// --- VERİTABANI BAĞLANTISI KODU (DEĞİŞMEDİ) ---
// ... (önceki doğru çalışan bağlantı kodu burada) ...
string? connectionString = null;
string? determinedHost = null;
int determinedPort = 5432;
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

earlyLogger.LogInformation("Okunan DATABASE_URL: {DatabaseUrl}", databaseUrl ?? "BULUNAMADI");

if (!string.IsNullOrEmpty(databaseUrl))
{
    earlyLogger.LogInformation("DATABASE_URL ortam değişkeni bulundu, ayrıştırma deneniyor.");
    try
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        if (userInfo.Length == 2)
        {
            determinedHost = uri.Host;
            determinedPort = uri.Port > 0 ? uri.Port : 5432;
            connectionString = $"Host={determinedHost};" +
                               $"Port={determinedPort};" +
                               $"Database={uri.LocalPath.TrimStart('/')};" +
                               $"Username={userInfo[0]};" +
                               $"Password={userInfo[1]};" +
                               "SSL Mode=Require;" +
                               "Trust Server Certificate=true;";
            earlyLogger.LogInformation("DATABASE_URL başarıyla ayrıştırıldı. Host: {Host}, Port: {Port}", determinedHost, determinedPort);
        }
        else
        {
             earlyLogger.LogError("HATA: DATABASE_URL formatı geçersiz (kullanıcı adı/şifre eksik?). Ayrıştırılan UserInfo: {UserInfo}", uri.UserInfo);
        }
    }
    catch (Exception ex)
    {
        earlyLogger.LogError(ex, "HATA: DATABASE_URL ayrıştırılırken istisna oluştu.");
    }
}

if (string.IsNullOrEmpty(connectionString))
{
    earlyLogger.LogWarning("DATABASE_URL kullanılamadı veya yok, appsettings.json'daki DefaultConnection deneniyor.");
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (!string.IsNullOrEmpty(connectionString))
    {
        try
        {
            var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            determinedHost = csBuilder.Host;
            determinedPort = csBuilder.Port;
            earlyLogger.LogInformation("appsettings.json'dan DefaultConnection okundu. Host: {Host}, Port: {Port}", determinedHost, determinedPort);
        }
        catch (Exception ex)
        {
             earlyLogger.LogError(ex, "HATA: appsettings.json'daki DefaultConnection ayrıştırılırken istisna oluştu.");
             connectionString = null;
        }
    }
    else
    {
        earlyLogger.LogWarning("appsettings.json içinde 'DefaultConnection' bulunamadı veya boş.");
    }
}

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(determinedHost))
{
    var errorMessage = "Kritik Hata: Geçerli bir veritabanı bağlantı dizesi veya host adı oluşturulamadı. Uygulama başlatılamıyor.";
    earlyLogger.LogCritical(errorMessage);
    throw new InvalidOperationException(errorMessage);
}

earlyLogger.LogInformation("DbContext için kullanılacak Host: {DeterminedHost}", determinedHost);
earlyLogger.LogInformation("DbContext için kullanılacak Port: {DeterminedPort}", determinedPort);
earlyLogger.LogInformation("DbContext için kullanılacak Bağlantı Dizesi (Şifre Gizli): {ConnectionString}",
    string.Join(";", connectionString.Split(';').Where(part => !part.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase))));


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- VERİTABANI BAĞLANTISI KODU SONU ---


// --- Identity, MVC, CORS (DEĞİŞMEDİ) ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


var app = builder.Build(); // Uygulamayı oluştur


// *** OTOMATİK MIGRATION UYGULAMA BAŞLANGICI ***
// ÖNEMLİ UYARI: Production ortamında otomatik migration riskli olabilir.
// Başarısız olursa veya uzun sürerse uygulama başlamayabilir.
// Daha güvenli yöntemler için (SQL script oluşturma gibi) EF Core dokümantasyonuna bakın.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Bekleyen veritabanı migration'ları uygulanıyor...");
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        // Bekleyen tüm migration'ları uygula
        dbContext.Database.Migrate();
        logger.LogInformation("Veritabanı migration'ları başarıyla uygulandı veya güncel.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Veritabanı migration'ları uygulanırken kritik bir hata oluştu.");
        // Hata durumunda uygulamanın başlamasını engellemek isteyebilirsiniz.
        // throw;
    }
}
// *** OTOMATİK MIGRATION UYGULAMA SONU ***


// Seed işlemi (DEĞİŞMEDİ)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Seed işlemi başlıyor...");
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "Admin", "User", "Editor", "Moderator" };

        foreach (var roleName in roles)
        {
            logger.LogDebug("'{RoleName}' rolü kontrol ediliyor...", roleName);
            if (!await roleMgr.RoleExistsAsync(roleName))
            {
                logger.LogInformation("'{RoleName}' rolü mevcut değil, oluşturuluyor...", roleName);
                var roleResult = await roleMgr.CreateAsync(new IdentityRole(roleName));
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("'{RoleName}' rolü başarıyla oluşturuldu.", roleName);
                }
                else
                {
                    logger.LogError("'{RoleName}' rolü oluşturulamadı: {Errors}", roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
             else {
                 logger.LogDebug("'{RoleName}' rolü zaten mevcut.", roleName);
             }
        }

        const string adminEmail = "denizvurgun58@gmail.com";
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "hyOhu>64;*35";
        logger.LogDebug("Admin kullanıcısı '{AdminEmail}' kontrol ediliyor...", adminEmail);
        var adminUser = await userMgr.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
             logger.LogInformation("Admin kullanıcısı '{AdminEmail}' mevcut değil, oluşturuluyor...", adminEmail);
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Site Admin"
            };

            var createUserResult = await userMgr.CreateAsync(adminUser, adminPassword);
            if (createUserResult.Succeeded)
            {
                logger.LogInformation("Admin kullanıcısı '{AdminEmail}' başarıyla oluşturuldu.", adminEmail);
                 logger.LogDebug("Admin kullanıcısı '{AdminEmail}' 'Admin' rolüne ekleniyor...", adminEmail);
                var addToRoleResult = await userMgr.AddToRoleAsync(adminUser, "Admin");
                 if (addToRoleResult.Succeeded)
                 {
                     logger.LogInformation("Admin kullanıcısı '{AdminEmail}' 'Admin' rolüne başarıyla eklendi.", adminEmail);
                 }
                 else
                 {
                     logger.LogError("Admin kullanıcısı '{AdminEmail}' 'Admin' rolüne eklenemedi: {Errors}", adminEmail, string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));
                 }
            }
            else
            {
                 logger.LogError("Admin kullanıcısı '{AdminEmail}' oluşturulamadı: {Errors}", adminEmail, string.Join(", ", createUserResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            logger.LogInformation("Admin kullanıcısı '{AdminEmail}' zaten mevcut.", adminEmail);
             logger.LogDebug("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolünde mi kontrol ediliyor...", adminEmail);
            if (!await userMgr.IsInRoleAsync(adminUser, "Admin"))
            {
                 logger.LogInformation("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolünde değil, ekleniyor...", adminEmail);
                var addToRoleResult = await userMgr.AddToRoleAsync(adminUser, "Admin");
                if (addToRoleResult.Succeeded)
                {
                    logger.LogInformation("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolüne başarıyla eklendi.", adminEmail);
                }
                else
                {
                    logger.LogError("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolüne eklenemedi: {Errors}", adminEmail, string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));
                }
            }
             else
            {
                 logger.LogInformation("Mevcut admin kullanıcısı '{AdminEmail}' zaten 'Admin' rolünde.", adminEmail);
            }
        }
         logger.LogInformation("Seed işlemi tamamlandı.");
    }
    catch (Exception ex)
    {
        // Seed işlemi sırasında oluşan hataları yakala (Artık migration hatası olmamalı)
        logger.LogCritical(ex, "Seed işlemi sırasında kritik bir hata oluştu.");
    }
}


// Middleware pipeline (DEĞİŞMEDİ)
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Routes (DEĞİŞMEDİ)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
