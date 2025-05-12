using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using HumanBodyWeb.Data;
using HumanBodyWeb.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- VERİTABANI BAĞLANTISI GÜNCELLEMESİ BAŞLANGICI ---

// 1) PostgreSQL DbContext - Ortam Değişkeni veya appsettings.json'dan Bağlantı Dizesi Al
string? connectionString = null; // Değişkeni nullable (string?) olarak tanımla ve başlangıçta null ata
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL"); // Render tarafından sağlanan değişkeni oku

if (!string.IsNullOrEmpty(databaseUrl))
{
    // DATABASE_URL formatı: postgres://user:password@host:port/database
    // Npgsql formatına dönüştür: Host=host;Port=port;Database=database;Username=user;Password=password;SSL Mode=Require;Trust Server Certificate=true;
    Console.WriteLine("DATABASE_URL ortam değişkeni bulundu, kullanılıyor.");
    try // URI ayrıştırma hata verebilir, try-catch içine alalım
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        if (userInfo.Length == 2) // Kullanıcı adı ve şifre var mı kontrol et
        {
            connectionString = $"Host={uri.Host};" +
                               $"Port={uri.Port};" +
                               $"Database={uri.LocalPath.TrimStart('/')};" +
                               $"Username={userInfo[0]};" +
                               $"Password={userInfo[1]};" +
                               "SSL Mode=Require;" + // Render genellikle SSL gerektirir
                               "Trust Server Certificate=true;"; // Basitlik için sunucu sertifikasına güven
        }
        else
        {
             Console.WriteLine("HATA: DATABASE_URL formatı geçersiz (kullanıcı adı/şifre eksik?).");
             // Fallback to appsettings.json or throw error later if appsettings is also missing
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA: DATABASE_URL ayrıştırılırken hata oluştu: {ex.Message}");
        // Fallback to appsettings.json or throw error later if appsettings is also missing
    }
}

// Eğer DATABASE_URL işlenemedi veya yoksa, appsettings.json'a bak
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("DATABASE_URL ortam değişkeni kullanılamadı veya yok, appsettings.json'daki DefaultConnection deneniyor.");
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection"); // Burası null dönebilir
}

// Son kontrol: Bağlantı dizesi hala boş mu?
if (string.IsNullOrEmpty(connectionString))
{
    // Hem ortam değişkeni hem de appsettings.json başarısız olduysa hata ver
    throw new InvalidOperationException("Veritabanı bağlantı dizesi bulunamadı. DATABASE_URL ortam değişkenini veya appsettings.json içinde 'DefaultConnection' girdisini kontrol edin.");
}


Console.WriteLine("Kullanılacak Bağlantı Dizesi => " + connectionString.Split(';').First(s => s.StartsWith("Host=")) + ";... (Şifre gizlendi)"); // Loglarken şifreyi gizle

// DbContext'i yapılandırılmış bağlantı dizesiyle ekle
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)); // connectionString artık null olamaz

// --- VERİTABANI BAĞLANTISI GÜNCELLEMESİ SONU ---


// 2) Identity (ApplicationUser + Roles + Token + UI)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

// 3) MVC + Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ✅ 4) CORS Ekle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 5) Roller ve Admin kullanıcıyı seed et (Bu kısım aynı kalabilir, loglamalar iyileştirildi)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>(); // Loglama için ILogger kullan
    try
    {
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "Admin", "User", "Editor", "Moderator" };

        foreach (var roleName in roles)
        {
            if (!await roleMgr.RoleExistsAsync(roleName)) // await kullan
            {
                var roleResult = await roleMgr.CreateAsync(new IdentityRole(roleName)); // await kullan
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("'{RoleName}' rolü oluşturuldu.", roleName);
                }
                else
                {
                    logger.LogError("'{RoleName}' rolü oluşturulamadı: {Errors}", roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }

        const string adminEmail = "denizvurgun58@gmail.com";
        // DİKKAT: Şifreyi kodda tutmak GÜVENSİZDİR! Ortam değişkeni veya Secret Manager kullanın.
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "hyOhu>64;*35"; // Önce ortam değişkenine bak

        var adminUser = await userMgr.FindByEmailAsync(adminEmail); // await kullan

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true, // Genellikle başlangıçta false olur, onay mekanizması varsa
                FullName = "Site Admin"
            };

            var createUserResult = await userMgr.CreateAsync(adminUser, adminPassword); // await kullan
            if (createUserResult.Succeeded)
            {
                logger.LogInformation("Admin kullanıcısı '{AdminEmail}' başarıyla oluşturuldu.", adminEmail);
                // Admin rolüne ekleme
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
            if (!await userMgr.IsInRoleAsync(adminUser, "Admin")) // await kullan
            {
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
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Seed işlemi sırasında kritik bir hata oluştu.");
        // Uygulamanın bu noktada durması gerekebilir, çünkü temel roller/kullanıcılar oluşturulamadı.
        // throw; // Hatayı tekrar fırlatarak uygulamanın başlamasını engelleyebilirsiniz.
    }
}


// Middleware pipeline (Bu kısım aynı kalabilir)
// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Home/Error");
//     app.UseHsts();
// }

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll"); // CORS middleware'ini UseRouting ve UseAuthentication/UseAuthorization arasına koymak iyi bir pratiktir.

app.UseAuthentication(); // Önce kimlik doğrulama
app.UseAuthorization(); // Sonra yetkilendirme

// 7) Route’lar (Bu kısım aynı kalabilir)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages(); // Razor Pages için endpointleri haritala

// Uygulamanın veritabanına erişebildiğini kontrol etmek için basit bir test (İsteğe bağlı ama faydalı)
// Bu test, uygulamanın başlangıcını yavaşlatabilir, dikkatli kullanın.
// try
// {
//     using (var scope = app.Services.CreateScope())
//     {
//         var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
//         var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
//         // Çok basit bir sorgu ile bağlantıyı test et
//         var canConnect = await dbContext.Database.CanConnectAsync(); // Asenkron kullan
//         if (canConnect)
//         {
//             logger.LogInformation("Veritabanı bağlantısı testi başarılı!");
//         }
//         else
//         {
//             logger.LogWarning("UYARI: Veritabanına bağlanılamadı (CanConnectAsync false döndü)!");
//         }
//     }
// }
// catch (Exception ex)
// {
//      var logger = app.Services.GetRequiredService<ILogger<Program>>(); // app.Services'dan logger al
//      logger.LogError(ex, "HATA: Veritabanı bağlantısı testi sırasında istisna oluştu.");
// }


app.Run(); // Uygulamayı çalıştır
