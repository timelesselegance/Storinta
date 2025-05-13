using System;
using CloudinaryDotNet; // Cloudinary için eklendi
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using HumanBodyWeb.Data; // Model ve DbContext namespace'lerinize göre güncelleyin
using HumanBodyWeb.Models; // Model ve DbContext namespace'lerinize göre güncelleyin
using Microsoft.EntityFrameworkCore;
using Npgsql; // NpgsqlConnectionStringBuilder için eklendi
// ILogger ve LoggerFactory için using ifadeleri zaten System ve Microsoft.Extensions.Logging altında olmalı
// Eğer değillerse:
// using Microsoft.Extensions.Logging;


var builder = WebApplication.CreateBuilder(args);

// Erken loglama için fabrika ve logger
var loggerFactory = LoggerFactory.Create(logBuilder =>
{
    logBuilder.AddConsole();
    // İsteğe bağlı: Diğer log sağlayıcıları eklenebilir
});
var earlyLogger = loggerFactory.CreateLogger<Program>(); // "Program" veya uygun bir kategori adı

earlyLogger.LogInformation("Uygulama başlatılıyor...");

// --- VERİTABANI BAĞLANTISI KODU ---
string? connectionString = null;
string? determinedHost = null; // Host adını saklamak için
int determinedPort = 5432; // Varsayılan port
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

earlyLogger.LogInformation("Okunan DATABASE_URL: {DatabaseUrl}", string.IsNullOrEmpty(databaseUrl) ? "BULUNAMADI" : "****"); // URL'yi loglamamak daha güvenli olabilir

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
            determinedPort = uri.Port > 0 ? uri.Port : 5432; // Sağlanmışsa portu al, yoksa varsayılan
            connectionString = $"Host={determinedHost};" +
                               $"Port={determinedPort};" +
                               $"Database={uri.LocalPath.TrimStart('/')};" +
                               $"Username={userInfo[0]};" +
                               $"Password={userInfo[1]};" + // Şifre burada!
                               "SSL Mode=Require;" + // Render için genellikle gerekli
                               "Trust Server Certificate=true;"; // Render için genellikle gerekli
            earlyLogger.LogInformation("DATABASE_URL başarıyla ayrıştırıldı. Host: {Host}, Port: {Port}", determinedHost, determinedPort);
        }
        else
        {
            earlyLogger.LogError("HATA: DATABASE_URL formatı geçersiz (kullanıcı adı/şifre eksik veya format yanlış). UserInfo: {UserInfo}", uri.UserInfo);
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
            // Bağlantı dizesinden host ve portu ayrıştırma (loglama ve kontrol için)
            var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            determinedHost = csBuilder.Host;
            determinedPort = csBuilder.Port;
            earlyLogger.LogInformation("appsettings.json'dan DefaultConnection başarıyla okundu. Host: {Host}, Port: {Port}", determinedHost, determinedPort);
        }
        catch (Exception ex)
        {
            earlyLogger.LogError(ex, "HATA: appsettings.json'daki DefaultConnection ayrıştırılırken istisna oluştu.");
            connectionString = null; // Hata durumunda bağlantı dizesini sıfırla
        }
    }
    else
    {
        earlyLogger.LogWarning("appsettings.json içinde 'DefaultConnection' bulunamadı veya boş.");
    }
}

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(determinedHost))
{
    var errorMessage = "Kritik Hata: Geçerli bir veritabanı bağlantı dizesi veya host adı oluşturulamadı. Uygulama düzgün başlatılamıyor.";
    earlyLogger.LogCritical(errorMessage);
    // Bu noktada uygulamanın çökmesi daha doğru olabilir, çünkü veritabanı olmadan çoğu işlev çalışmayacaktır.
    throw new InvalidOperationException(errorMessage);
}

// Şifreyi loglamadan bağlantı dizesini logla
var safeConnectionStringForLogging = string.Join(";", connectionString.Split(';').Where(part => !part.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase)));
earlyLogger.LogInformation("DbContext için kullanılacak Host: {DeterminedHost}, Port: {DeterminedPort}", determinedHost, determinedPort);
earlyLogger.LogInformation("DbContext için kullanılacak Bağlantı Dizesi (Şifre Gizli): {ConnectionString}", safeConnectionStringForLogging);


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- VERİTABANI BAĞLANTISI KODU SONU ---


// --- Identity, MVC, CORS Servisleri ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // Proje gereksinimlerinize göre ayarlayın
        // Diğer Identity ayarları buraya eklenebilir
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Razor Pages kullanıyorsanız

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => // Daha kısıtlayıcı bir policy kullanmanız önerilir
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


// --- BAŞLANGIÇ: Cloudinary Yapılandırması ---
// CLOUDINARY_URL'i ortam değişkenlerinden okuma (Render ve diğer hosting platformları için en iyi pratik).
string? cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL"); // <--- DEĞİŞİKLİK BURADA (string? oldu)

if (string.IsNullOrEmpty(cloudinaryUrl))
{
    earlyLogger.LogWarning("CLOUDINARY_URL ortam değişkeni bulunamadı, appsettings.json'daki 'CloudinaryUrl' anahtarı deneniyor.");
    // "CloudinaryUrl" anahtarının appsettings.json dosyanızda olduğundan emin olun
    // Eğer "CloudinarySettings:CloudinaryUrl" gibi bir yol kullanıyorsanız burayı güncelleyin
    cloudinaryUrl = builder.Configuration["CloudinaryUrl"]; // Bu satırda artık hata olmamalı
    if (!string.IsNullOrEmpty(cloudinaryUrl)) {
        earlyLogger.LogInformation("CLOUDINARY_URL ortam değişkeni bulunamadı, appsettings.json'dan okundu.");
    }
} else {
    earlyLogger.LogInformation("CLOUDINARY_URL ortam değişkeninden başarıyla okundu.");
}

// CLOUDINARY_URL'in varlığını kontrol edin (string.IsNullOrEmpty zaten null durumunu kontrol eder)
if (string.IsNullOrEmpty(cloudinaryUrl))
{
    earlyLogger.LogError("KRİTİK HATA: CLOUDINARY_URL ortam değişkeni veya yapılandırma ayarı bulunamadı. Lütfen CLOUDINARY_URL değerini 'cloudinary://API_KEY:API_SECRET@CLOUD_NAME' formatında ayarlayın. Görsel yükleme özelliği çalışmayacaktır.");
    // throw new InvalidOperationException("CLOUDINARY_URL yapılandırılmamış."); // Uygulamanın başlamasını engellemek için bu satırı aktif edebilirsiniz.
}
else
{
    try
    {
        // cloudinaryUrl burada null olamaz çünkü string.IsNullOrEmpty kontrolünden geçti.
        // Ancak derleyici bunu %100 bilemeyebilir, bu yüzden null-forgiving operatörü (!) gerekebilir veya
        // Cloudinary constructor'ının string? kabul edip etmediğini kontrol etmek gerekir.
        // Cloudinary(string url) constructor'ı null kabul etmez.
        earlyLogger.LogInformation("CLOUDINARY_URL bulundu, Cloudinary servisi yapılandırılıyor: {CloudinaryUrl}", "****"); // URL'yi loglamamak daha güvenli
        Cloudinary cloudinary = new Cloudinary(cloudinaryUrl); // cloudinaryUrl burada null değil.
        
        builder.Services.AddSingleton(cloudinary);
        earlyLogger.LogInformation("Cloudinary servisi başarıyla yapılandırıldı ve DI container'a eklendi.");
    }
    catch (Exception ex)
    {
        earlyLogger.LogError(ex, "Cloudinary servisi yapılandırılırken bir istisna oluştu. CLOUDINARY_URL: {CloudinaryUrl}", "****");
    }
}
// --- BİTİŞ: Cloudinary Yapılandırması ---

var app = builder.Build(); // Uygulamayı oluştur


// --- Middleware ve Uygulama Başlangıç İşlemleri ---

// OTOMATİK MIGRATION UYGULAMA BAŞLANGICI
// UYARI: Production ortamında otomatik migration dikkatli kullanılmalıdır.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var migrationLogger = services.GetRequiredService<ILogger<Program>>(); // Scope'a özel logger
    try
    {
        migrationLogger.LogInformation("Veritabanı migration'ları kontrol ediliyor ve uygulanıyor...");
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate(); // Bekleyen tüm migration'ları uygula
        migrationLogger.LogInformation("Veritabanı migration'ları başarıyla uygulandı veya veritabanı güncel.");
    }
    catch (Exception ex)
    {
        migrationLogger.LogCritical(ex, "Veritabanı migration'ları uygulanırken kritik bir hata oluştu.");
        // Hata durumunda uygulamanın başlamasını engellemek daha güvenli olabilir.
        // throw;
    }
}
// OTOMATİK MIGRATION UYGULAMA SONU


// SEED İŞLEMİ BAŞLANGICI
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var seedLogger = services.GetRequiredService<ILogger<Program>>(); // Scope'a özel logger
    try
    {
        seedLogger.LogInformation("Seed işlemi başlıyor...");
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "Admin", "User", "Editor", "Moderator" }; // Projenize göre rolleri düzenleyin

        foreach (var roleName in roles)
        {
            seedLogger.LogDebug("'{RoleName}' rolü kontrol ediliyor...", roleName);
            if (!await roleMgr.RoleExistsAsync(roleName))
            {
                seedLogger.LogInformation("'{RoleName}' rolü mevcut değil, oluşturuluyor...", roleName);
                var roleResult = await roleMgr.CreateAsync(new IdentityRole(roleName));
                if (roleResult.Succeeded)
                {
                    seedLogger.LogInformation("'{RoleName}' rolü başarıyla oluşturuldu.", roleName);
                }
                else
                {
                    seedLogger.LogError("'{RoleName}' rolü oluşturulamadı: {Errors}", roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                seedLogger.LogDebug("'{RoleName}' rolü zaten mevcut.", roleName);
            }
        }

        // Admin kullanıcı oluşturma/kontrol etme
        const string adminEmail = "denizvurgun58@gmail.com"; // Admin e-posta adresiniz
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "DefaultAdminPassword123!"; // Güçlü bir varsayılan şifre kullanın ve ortam değişkeniyle ezin!
        
        seedLogger.LogDebug("Admin kullanıcısı '{AdminEmail}' kontrol ediliyor...", adminEmail);
        var adminUser = await userMgr.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            seedLogger.LogInformation("Admin kullanıcısı '{AdminEmail}' mevcut değil, oluşturuluyor...", adminEmail);
            adminUser = new ApplicationUser
            {
                UserName = adminEmail, // Veya farklı bir kullanıcı adı
                Email = adminEmail,
                EmailConfirmed = true, // Otomatik onaylı
                FullName = "Site Yöneticisi" // Örnek
                // Diğer ApplicationUser özelliklerini burada ayarlayın
            };

            var createUserResult = await userMgr.CreateAsync(adminUser, adminPassword);
            if (createUserResult.Succeeded)
            {
                seedLogger.LogInformation("Admin kullanıcısı '{AdminEmail}' başarıyla oluşturuldu.", adminEmail);
                seedLogger.LogDebug("Admin kullanıcısı '{AdminEmail}' 'Admin' rolüne ekleniyor...", adminEmail);
                var addToRoleResult = await userMgr.AddToRoleAsync(adminUser, "Admin"); // "Admin" rolünün yukarıda tanımlandığından emin olun
                if (addToRoleResult.Succeeded)
                {
                    seedLogger.LogInformation("Admin kullanıcısı '{AdminEmail}' 'Admin' rolüne başarıyla eklendi.", adminEmail);
                }
                else
                {
                    seedLogger.LogError("Admin kullanıcısı '{AdminEmail}' 'Admin' rolüne eklenemedi: {Errors}", adminEmail, string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                seedLogger.LogError("Admin kullanıcısı '{AdminEmail}' oluşturulamadı: {Errors}", adminEmail, string.Join(", ", createUserResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            seedLogger.LogInformation("Admin kullanıcısı '{AdminEmail}' zaten mevcut.", adminEmail);
            if (!await userMgr.IsInRoleAsync(adminUser, "Admin"))
            {
                seedLogger.LogInformation("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolünde değil, ekleniyor...", adminEmail);
                var addToRoleResult = await userMgr.AddToRoleAsync(adminUser, "Admin");
                if (addToRoleResult.Succeeded)
                {
                    seedLogger.LogInformation("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolüne başarıyla eklendi.", adminEmail);
                }
                else
                {
                    seedLogger.LogError("Mevcut admin kullanıcısı '{AdminEmail}' 'Admin' rolüne eklenemedi: {Errors}", adminEmail, string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                seedLogger.LogInformation("Mevcut admin kullanıcısı '{AdminEmail}' zaten 'Admin' rolünde.", adminEmail);
            }
        }
        seedLogger.LogInformation("Seed işlemi tamamlandı.");
    }
    catch (Exception ex)
    {
        seedLogger.LogCritical(ex, "Seed işlemi sırasında kritik bir hata oluştu.");
        // Bu hata, uygulamanın genel çalışmasını etkilemeyebilir, ancak loglanması önemlidir.
    }
}
// SEED İŞLEMİ SONU


// Middleware pipeline'ını yapılandırın
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Geliştirme ortamında detaylı hata sayfaları
    // app.UseMigrationsEndPoint(); // Eğer EF Core migration endpoint'i kullanılıyorsa
}
else
{
    app.UseExceptionHandler("/Home/Error"); // Üretim ortamı için genel hata sayfası
    app.UseHsts(); // HTTPS Strict Transport Security
}

app.UseHttpsRedirection(); // HTTP isteklerini HTTPS'ye yönlendir
app.UseStaticFiles(); // wwwroot klasöründeki statik dosyaların sunulmasını sağlar

app.UseRouting(); // Routing middleware'ini ekler

app.UseCors("AllowAll"); // CORS policy'sini uygula (Routing'den sonra, Authorization'dan önce)

app.UseAuthentication(); // Kimlik doğrulama middleware'ini ekler
app.UseAuthorization(); // Yetkilendirme middleware'ini ekler

// Route'ları tanımla
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages(); // Razor Pages kullanıyorsanız

earlyLogger.LogInformation("Uygulama çalışmaya hazır ve başlatılıyor...");
app.Run(); // Uygulamayı çalıştır