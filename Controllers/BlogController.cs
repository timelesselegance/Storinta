using System;
using System.IO; // Path işlemleri için
using System.Linq; // Linq sorguları için
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using HumanBodyWeb.Data;
using HumanBodyWeb.Models;
using HumanBodyWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting; // _env için, eğer başka yerde kullanılıyorsa kalsın
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet; // Cloudinary için eklendi
using CloudinaryDotNet.Actions; // Cloudinary action'ları için eklendi
using Microsoft.Extensions.Logging; // ILogger için


namespace HumanBodyWeb.Controllers
{
    [Authorize]
    [Route("blog")]
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _db;
        // IWebHostEnvironment _env; // Eğer sadece yerel dosya kaydetme için kullanılıyorsa ve bu kaldırıldıysa, _env de kaldırılabilir.
                                     // Şimdilik, başka bir amaçla kullanılıyor olabileceği varsayımıyla tutuyorum.
                                     // Eğer kullanılmıyorsa, constructor'dan ve bu alandan kaldırın.
        private readonly Cloudinary _cloudinary; 
        private readonly ILogger<BlogController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;



        public BlogController(
            ApplicationDbContext db,
            Cloudinary cloudinary,
            ILogger<BlogController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _cloudinary = cloudinary;
            _logger = logger;
            _userManager = userManager;
        }
        

        /* ---------- LIST (INDEX) ---------- */
        [AllowAnonymous, HttpGet("")]
        public async Task<IActionResult> Index(string? search, int? categoryId)
        {
            _logger.LogInformation("Blog yazıları listeleniyor. Arama: '{Search}', KategoriID: {CategoryId}", search, categoryId);
            var query = _db.BlogPosts
                            .Include(p => p.Author) 
                           .Include(p => p.Category)
                           // BlogPost entity'nizde public DateTime CreatedOn { get; set; } olmalı
                           .OrderByDescending(p => p.PublishedOn ?? p.CreatedOn) 
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Title.Contains(search) || p.Content.Contains(search));

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            var vm = new BlogListViewModel
            {
                SearchQuery = search,
                SelectedCategoryId = categoryId,
                Posts = await query.ToListAsync(),
                Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync()
            };

            return View(vm);
        }

        /* ---------- CREATE ---------- */
        [Authorize(Roles = "Editor,Admin")]
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var vm = new BlogPostFormVM
            {
                Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync()
            };
            // Create ve Edit için ortak bir form adı kullanıyorsanız:
            // return View("BlogPostForm", vm); 
            return View(vm); // Eğer Create.cshtml ve Edit.cshtml ayrı ise
        }
        [Authorize(Roles = "Editor,Admin")]
        [HttpPost("create"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogPostFormVM vm, bool isDraft = false)
        {
            vm.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync(); 

            if (vm.CategoryId == 0 && string.IsNullOrWhiteSpace(vm.NewCategoryName))
            {
                ModelState.AddModelError("CategoryId", "Lütfen bir kategori seçin veya yeni bir kategori adı girin.");
            }
            else if (vm.CategoryId == 0 && !string.IsNullOrWhiteSpace(vm.NewCategoryName))
            {
                // Yeni kategori oluşturma ve vm.CategoryId'yi atama (eski ResolveCategoryAsync mantığı)
                 vm.CategoryId = await ResolveCategoryAsync(0, vm.NewCategoryName);
                 if(vm.CategoryId == 0) // Kategori hala çözümlenemediyse (örn: DB hatası)
                 {
                    ModelState.AddModelError("NewCategoryName", "Yeni kategori oluşturulamadı.");
                 }
            }
            
            if (!ModelState.IsValid) return View(vm); // Eğer Create.cshtml ise, değilse "BlogPostForm"

            string? uploadedImageUrl = null;
            string? uploadedImagePublicId = null;

            if (vm.FeaturedImageFile != null && vm.FeaturedImageFile.Length > 0)
            {
                var uploadResult = await UploadImageToCloudinaryAsync(vm.FeaturedImageFile);
                if (uploadResult == null || string.IsNullOrEmpty(uploadResult.Value.SecureUrl))
                {
                    ModelState.AddModelError("FeaturedImageFile", "Görsel yüklenirken bir sorun oluştu veya dosya uygun değil. (Max 5MB, .jpg, .png, .gif)");
                    return View(vm); // Eğer Create.cshtml ise, değilse "BlogPostForm"
                }
                uploadedImageUrl = uploadResult.Value.SecureUrl;
                uploadedImagePublicId = uploadResult.Value.PublicId;
            }
           var currentUserId = _userManager.GetUserId(User)!; 

            var post = new BlogPost
            {
                Title = vm.Title,
                Slug = vm.Slug,
                Content = vm.Content,
                CategoryId = vm.CategoryId,
                FeaturedImageUrl = uploadedImageUrl,
                // BlogPost entity'nizde public string? FeaturedImagePublicId { get; set; } olmalı
                FeaturedImagePublicId = uploadedImagePublicId,
                IsDraft = isDraft,
                // BlogPost entity'nizde public DateTime CreatedOn { get; set; } olmalı
                CreatedOn = DateTime.UtcNow,
                PublishedOn = isDraft ? null : DateTime.UtcNow,
                AuthorId            = currentUserId
            };

            _db.BlogPosts.Add(post);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Yeni blog yazısı '{Title}' (ID: {PostId}) başarıyla oluşturuldu.", post.Title, post.Id);
            TempData["SuccessMessage"] = "Blog yazısı başarıyla oluşturuldu!";
            return RedirectToAction(nameof(Index));
        }

        /* ---------- DETAILS ---------- */
        [AllowAnonymous, HttpGet("{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            var post = await _db.BlogPosts
                                .Include(p => p.Author)
                                .Include(p => p.Category)
                                .FirstOrDefaultAsync(p => p.Slug == slug);
            if (post == null)
            {
                _logger.LogWarning("Detayları görüntülenmek istenen blog yazısı bulunamadı. Slug: {Slug}", slug);
                return NotFound();
            }
            return View(post);
        }

      /* ---------- EDIT ---------- */
[Authorize(Roles = "Editor,Admin")]
[HttpGet("edit/{id:int}")]
public async Task<IActionResult> Edit(int id)
{
    var post = await _db.BlogPosts.FindAsync(id);
    if (post == null)
    {
        _logger.LogWarning("Düzenlenmek istenen blog yazısı bulunamadı. ID: {PostId}", id);
        return NotFound();
    }

    // ——————————————————————————
    // Sahiplik kontrolü: 
    // Editör yalnızca kendi yazısını, Admin ise herkesi düzenleyebilir
    var currentUserId = _userManager.GetUserId(User);
   if (post.AuthorId != currentUserId && !User.IsInRole("Admin"))
    return Forbid();
    // ——————————————————————————

    var vm = BlogPostFormVM.FromEntity(post);
    vm.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
    return View(vm);
}


 [Authorize(Roles = "Editor,Admin")]
[HttpPost("edit/{id:int}"), ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, BlogPostFormVM vm)
{
    // 1. ID uyuşmazlığı
    if (id != vm.Id)
        return BadRequest("ID uyuşmazlığı.");

    // 2. Mevcut post’u çek
    var post = await _db.BlogPosts.FindAsync(id);
    if (post == null)
        return NotFound();

    // 3. Sahiplik kontrolü (FORBID’ü return ile döndür!)
    var currentUserId = _userManager.GetUserId(User);
    if (post.AuthorId != currentUserId && !User.IsInRole("Admin"))
        return Forbid();

    // 4. Kategori listesini yeniden doldur
    vm.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();

    // 5. ModelState validasyonu
    if (vm.CategoryId == 0 && string.IsNullOrWhiteSpace(vm.NewCategoryName))
    {
        ModelState.AddModelError("CategoryId", "Lütfen bir kategori seçin veya yeni bir kategori adı girin.");
    }
    else if (vm.CategoryId == 0 && !string.IsNullOrWhiteSpace(vm.NewCategoryName))
    {
        vm.CategoryId = await ResolveCategoryAsync(0, vm.NewCategoryName);
        if (vm.CategoryId == 0)
            ModelState.AddModelError("NewCategoryName", "Yeni kategori oluşturulamadı.");
    }

    if (!ModelState.IsValid)
        return View(vm);

    // 6. Güncelleme mantığı (resim sil/yükle, alan atamaları…)
    //    ... (post.Title = vm.Title; vs.)

    try
    {
        _db.Update(post);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Blog yazısı başarıyla güncellendi!";
    }
    catch (DbUpdateConcurrencyException)
    {
        ModelState.AddModelError(string.Empty, "Eş zamanlılık hatası. Lütfen sayfayı yeniden yükleyip tekrar deneyin.");
        return View(vm);
    }

    // 7. Sonuçta Redirect ile dön
    return RedirectToAction(nameof(Details), new { slug = post.Slug });
}


        /* ---------- DELETE ---------- */
        [Authorize(Roles = "Editor,Admin")]
        [HttpPost("delete/{id:int}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // 1. Gönderiyi çek
            var post = await _db.BlogPosts.FindAsync(id);
            if (post == null)
            {
                _logger.LogWarning("Silinmek istenen blog yazısı bulunamadı. ID: {PostId}", id);
                TempData["ErrorMessage"] = "Silinecek blog yazısı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // 2. Sahiplik kontrolü
            var currentUserId = _userManager.GetUserId(User);
            if (post.AuthorId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            // 3. Var ise Cloudinary görselini sil
            if (!string.IsNullOrEmpty(post.FeaturedImagePublicId))
            {
                await DeleteImageFromCloudinaryAsync(post.FeaturedImagePublicId);
            }

            // 4. Kaydı sil ve kaydet
            _db.BlogPosts.Remove(post);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Blog yazısı '{Title}' (ID: {PostId}) silindi.", post.Title, post.Id);
            TempData["SuccessMessage"] = "Blog yazısı başarıyla silindi!";

            return RedirectToAction(nameof(Index));
        }



        /* ---------- HELPERS ---------- */
        private async Task<int> ResolveCategoryAsync(int selectedId, string? newName)
        {
            if (selectedId > 0 && string.IsNullOrWhiteSpace(newName)) return selectedId;

            if (!string.IsNullOrWhiteSpace(newName))
            {
                var trimmedNewName = newName.Trim();
                var existing = await _db.Categories
                                        .FirstOrDefaultAsync(c => c.Name.Equals(trimmedNewName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing.Id;

                var slug = trimmedNewName.ToLowerInvariant()
                                     .Replace(" ", "-")
                                     .Replace("ı", "i").Replace("İ", "i")
                                     .Replace("ö", "o").Replace("Ö", "o")
                                     .Replace("ü", "u").Replace("Ü", "u")
                                     .Replace("ş", "s").Replace("Ş", "s")
                                     .Replace("ğ", "g").Replace("Ğ", "g")
                                     .Replace("ç", "c").Replace("Ç", "c")
                                     .Replace("#", "sharp")
                                     .Replace("+", "plus");
                // Basit slug oluşturma, daha gelişmiş kütüphaneler kullanılabilir
                slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", ""); // Sadece harf, rakam ve tire
                slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-{2,}", "-"); // Çoklu tireleri teke indir
                slug = slug.Trim('-'); // Baş ve sondaki tireleri kaldır


                var cat = new Category { Name = trimmedNewName, Slug = slug };
                try
                {
                    _db.Categories.Add(cat);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Yeni kategori '{CategoryName}' (ID: {CategoryId}) oluşturuldu.", cat.Name, cat.Id);
                    return cat.Id;
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Yeni kategori '{CategoryName}' oluşturulurken veritabanı hatası.", trimmedNewName);
                    return 0; // Hata durumunda 0 dön
                }
            }
            return selectedId > 0 ? selectedId : 0; // Eğer selectedId geçerliyse onu, değilse 0 dön
        }

        private async Task<(string? SecureUrl, string? PublicId)?> UploadImageToCloudinaryAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogInformation("Cloudinary'ye yüklemek için dosya sağlanmadı.");
                return null;
            }

            if (file.Length > 5 * 1024 * 1024) // 5 MB limit
            {
                _logger.LogWarning("Cloudinary'ye yüklenecek dosya boyutu limiti (5MB) aştı: {FileName}, Boyut: {FileSize} bytes", file.FileName, file.Length);
                return null;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Cloudinary'ye yüklenmek istenen dosya türü geçersiz (izin verilenler: {AllowedExtensions}): {FileName}, Uzantı: {FileExtension}", string.Join(", ", allowedExtensions), file.FileName, extension);
                return null;
            }

            _logger.LogInformation("'{FileName}' Cloudinary'ye yükleniyor...", file.FileName);
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "blog_images", 
                        // Transformation = new Transformation().Width(1200).Crop("limit") 
                    };
                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.Error != null)
                    {
                        _logger.LogError("Cloudinary görsel yükleme hatası: {Error}. Dosya: {FileName}", uploadResult.Error.Message, file.FileName);
                        return null;
                    }

                    _logger.LogInformation("'{FileName}' başarıyla Cloudinary'ye yüklendi. URL: {ImageUrl}, PublicID: {PublicId}",
                        file.FileName, uploadResult.SecureUrl.ToString(), uploadResult.PublicId);
                    return (uploadResult.SecureUrl.ToString(), uploadResult.PublicId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary'ye görsel yüklenirken beklenmedik hata oluştu: {FileName}", file.FileName);
                return null;
            }
        }

        private async Task DeleteImageFromCloudinaryAsync(string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
            {
                _logger.LogWarning("Cloudinary'den silinmek üzere geçersiz veya boş Public ID sağlandı.");
                return;
            }

            try
            {
                var deletionParams = new DeletionParams(publicId);
                var deletionResult = await _cloudinary.DestroyAsync(deletionParams);

                if (deletionResult.Result.Equals("ok", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("'{PublicId}' Public ID'li görsel Cloudinary'den başarıyla silindi.", publicId);
                }
                else if (deletionResult.Result.Equals("not found", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("'{PublicId}' Public ID'li görsel Cloudinary'de bulunamadı, silinemedi. Result: {Result}", publicId, deletionResult.Result);
                }
                else
                {
                    _logger.LogError("'{PublicId}' Public ID'li görsel Cloudinary'den silinirken hata oluştu. Result: {Result}, Error: {Error}", publicId, deletionResult.Result, deletionResult.Error?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "'{PublicId}' Public ID'li görsel Cloudinary'den silinirken beklenmedik bir hata oluştu.", publicId);
            }
        }
    }
}