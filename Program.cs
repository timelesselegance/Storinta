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

// --- VERİTABANI BAĞLANTISI GÜNCELLEMESİ BAŞLANGICI ---

// 1) PostgreSQL DbContext - Ortam Değişkeni veya appsettings.json'dan Bağlantı Dizesi Al
string? connectionString = null; // Değişkeni nullable (string?) olarak tanımla ve başlangıçta null ata
string? determinedHost = null; // Hangi hostun kullanıldığını loglamak için
int determinedPort = 5432; // Varsayılan port
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL"); // Render tarafından sağlanan değişkeni oku

earlyLogger.LogInformation("Okunan DATABASE_URL: {DatabaseUrl}", databaseUrl ?? "BULUNAMADI"); // Okunan URL'yi logla

if (!string.IsNullOrEmpty(databaseUrl))
{
    earlyLogger.LogInformation("DATABASE_URL ortam değişkeni bulundu, ayrıştırma deneniyor.");
    try // URI ayrıştırma hata verebilir, try-catch içine alalım
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        if (userInfo.Length == 2) // Kullanıcı adı ve şifre var mı kontrol et
        {
            determinedHost = uri.Host; // Kullanılacak host'u belirle

            // *** PORT DÜZELTMESİ BAŞLANGICI ***
            // Eğer URL'de port belirtilmemişse uri.Port -1 döner, bu durumda varsayılan 5432'yi kullan.
            determinedPort = uri.Port > 0 ? uri.Port : 5432;
            // *** PORT DÜZELTMESİ SONU ***

            connectionString = $"Host={determinedHost};" +
                               $"Port={determinedPort};" + // Düzeltilmiş portu kullan
                               $"Database={uri.LocalPath.TrimStart('/')};" +
                               $"Username={userInfo[0]};" +
                               $"Password={userInfo[1]};" +
                               "SSL Mode=Require;" + // Render genellikle SSL gerektirir
                               "Trust Server Certificate=true;"; // Basitlik için sunucu sertifikasına güven
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
else
{
    earlyLogger.LogWarning("DATABASE_URL ortam değişkeni bulunamadı.");
}

// Eğer DATABASE_URL işlenemedi veya yoksa, appsettings.json'a bak
if (string.IsNullOrEmpty(connectionString))
{
    earlyLogger.LogWarning("DATABASE_URL kullanılamadı veya yok, appsettings.json'daki DefaultConnection deneniyor.");
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection"); // Burası null dönebilir

    if (!string.IsNullOrEmpty(connectionString))
    {
        try
        {
            // appsettings'den host ve port'u ayrıştırıp loglayalım
            var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            determinedHost = csBuilder.Host;
            determinedPort = csBuilder.Port; // appsettings'deki portu al
            earlyLogger.LogInformation("appsettings.json'dan DefaultConnection okundu. Host: {Host}, Port: {Port}", determinedHost, determinedPort);
        }
        catch (Exception ex)
        {
             earlyLogger.LogError(ex, "HATA: appsettings.json'daki DefaultConnection ayrıştırılırken istisna oluştu.");
             connectionString = null; // Hatalıysa kullanma
        }
    }
    else
    {
        earlyLogger.LogWarning("appsettings.json içinde 'DefaultConnection' bulunamadı veya boş.");
    }
}

// Son kontrol: Bağlantı dizesi hala boş mu?
if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(determinedHost))
{
    var errorMessage = "Kritik Hata: Geçerli bir veritabanı bağlantı dizesi veya host adı oluşturulamadı. Uygulama başlatılamıyor.";
    earlyLogger.LogCritical(errorMessage);
    throw new InvalidOperationException(errorMessage);
}

earlyLogger.LogInformation("DbContext için kullanılacak Host: {DeterminedHost}", determinedHost);
earlyLogger.LogInformation("DbContext için kullanılacak Port: {DeterminedPort}", determinedPort); // Portu da logla
earlyLogger.LogInformation("DbContext için kullanılacak Bağlantı Dizesi (Şifre Gizli): {ConnectionString}",
    string.Join(";", connectionString.Split(';').Where(part => !part.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase))));


// DbContext'i yapılandırılmış bağlantı dizesiyle ekle
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)); // connectionString artık null olamaz

// --- VERİTABANI BAĞLANTISI GÜNCELLEMESİ SONU ---


// --- GERİ KALAN KOD (Identity, MVC, CORS, Seed, Middleware, Routes, Run) ÖNCEKİ GİBİ DEVAM EDİYOR ---
// ... (önceki kod bloğundaki gibi) ...

// Örnek: Identity ekleme
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

// Örnek: MVC ekleme
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Örnek: CORS ekleme
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


// Seed işlemi (ILogger kullanacak şekilde güncellenmişti)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>(); // Doğru logger'ı al
    try
    {
        logger.LogInformation("Seed işlemi başlıyor..."); // Başlangıç logu
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "Admin", "User", "Editor", "Moderator" };

        foreach (var roleName in roles)
        {
            logger.LogDebug("'{RoleName}' rolü kontrol ediliyor...", roleName);
            if (!await roleMgr.RoleExistsAsync(roleName)) // await kullan
            {
                logger.LogInformation("'{RoleName}' rolü mevcut değil, oluşturuluyor...", roleName);
                var roleResult = await roleMgr.CreateAsync(new IdentityRole(roleName)); // await kullan
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
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "hyOhu>64;*35"; // Önce ortam değişkenine bak
        logger.LogDebug("Admin kullanıcısı '{AdminEmail}' kontrol ediliyor...", adminEmail);
        var adminUser = await userMgr.FindByEmailAsync(adminEmail); // await kullan

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

            var createUserResult = await userMgr.CreateAsync(adminUser, adminPassword); // await kullan
            if (createUserResult.Succeeded)
            {
                logger.LogInformation("Admin kullanıcısı '{AdminEmail}' başarıyla oluşturuldu.", adminEmail);
                // Admin rolüne ekleme
                 logger.LogDebug("Admin kullanıcısı '{AdminEmail}' 'Admin' rolüne ekleniyor...", adminEmail);
                var addToRoleResult = await userMgr.AddToRoleAsync(adminUser, "Admin"); // await kullan
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
             // Mevcut admin kullanıcısının Admin rolünde olup olmadığını kontrol et ve gerekirse ekle
             logger.LogDebug("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolünde mi kontrol ediliyor...", adminEmail);
            if (!await userMgr.IsInRoleAsync(adminUser, "Admin")) // await kullan
            {
                 logger.LogInformation("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolünde değil, ekleniyor...", adminEmail);
                var addToRoleResult = await userMgr.AddToRoleAsync(adminUser, "Admin"); // await kullan
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
         logger.LogInformation("Seed işlemi tamamlandı."); // Bitiş logu
    }
    catch (Exception ex) // Seed işlemi sırasında oluşan hataları yakala
    {
        // ÖNEMLİ: ArgumentException (port hatası) burada yakalanacak!
        logger.LogCritical(ex, "Seed işlemi sırasında kritik bir hata oluştu.");
        // Uygulamanın başlamasını engellemek için hatayı tekrar fırlatabiliriz
        // throw;
    }
}


// Middleware pipeline
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
