using System;
using System.IO;
using System.Threading.Tasks;
using HumanBodyWeb.Data;
using HumanBodyWeb.Models;
using HumanBodyWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HumanBodyWeb.Controllers
{
    [Authorize]
    [Route("blog")]
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment   _env;

        public BlogController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db  = db;
            _env = env;
        }

        /* ---------- LIST ---------- */
        [AllowAnonymous, HttpGet("")]
public async Task<IActionResult> Index(string? search, int? categoryId)
{
    var query = _db.BlogPosts
                   .Include(p => p.Category)
                   .OrderByDescending(p => p.PublishedOn)
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
        [HttpGet("create")]
public async Task<IActionResult> Create()
{
    var vm = new BlogPostFormVM
    {
        Categories = await _db.Categories.ToListAsync()
    };
    return View(vm);  // Create.cshtml
}


        [HttpPost("create"), ValidateAntiForgeryToken]
public async Task<IActionResult> Create(BlogPostFormVM vm, bool isDraft = false)
{
    vm.Categories = await _db.Categories.ToListAsync(); // geri dönebiliriz

    if (!ModelState.IsValid) return View(vm);

    // 1) kategori belirle / oluştur
    vm.CategoryId = await ResolveCategoryAsync(vm.CategoryId, vm.NewCategoryName);
    if (vm.CategoryId == 0)
    {
        ModelState.AddModelError("CategoryId", "Kategori seçin veya oluşturun.");
        return View(vm);
    }

    // 2) görsel
    vm.FeaturedImageUrl = await SaveFileAsync(vm.FeaturedImageFile, "featured");

    // 3) kaydet
    var post = new BlogPost
    {
        Title           = vm.Title,
        Slug            = vm.Slug,
        Content         = vm.Content,
        CategoryId      = vm.CategoryId,
        FeaturedImageUrl= vm.FeaturedImageUrl,
        IsDraft         = isDraft,
        PublishedOn     = isDraft ? null : DateTime.UtcNow
    };
    _db.BlogPosts.Add(post);
    await _db.SaveChangesAsync();
    return RedirectToAction(nameof(Index));
}


        /* ---------- DETAILS ---------- */
        [AllowAnonymous, HttpGet("{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            var post = await _db.BlogPosts
                                .Include(p => p.Category)
                                .FirstOrDefaultAsync(p => p.Slug == slug);
            return post == null ? NotFound() : View(post);
        }
        /* ---------- EDIT ---------- */
        [HttpGet("edit/{id:int}")]
public async Task<IActionResult> Edit(int id)
{
    var post = await _db.BlogPosts.FindAsync(id);
    if (post == null) return NotFound();

    var vm = BlogPostFormVM.FromEntity(post);
    vm.Categories = await _db.Categories.ToListAsync();
    return View(vm);   // Edit.cshtml
}


       [HttpPost("edit/{id:int}"), ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, BlogPostFormVM vm)
{
    vm.Categories = await _db.Categories.ToListAsync();
    if (!ModelState.IsValid) return View(vm);

    var post = await _db.BlogPosts.FindAsync(id);
    if (post == null) return NotFound();

    vm.CategoryId = await ResolveCategoryAsync(vm.CategoryId, vm.NewCategoryName);
    if (vm.CategoryId == 0)
    {
        ModelState.AddModelError("CategoryId", "Geçerli bir kategori seçin.");
        return View(vm);
    }

    var newImg = await SaveFileAsync(vm.FeaturedImageFile, "featured");
    if (newImg != null) post.FeaturedImageUrl = newImg;

    post.Title      = vm.Title;
    post.Slug       = vm.Slug;
    post.Content    = vm.Content;
    post.CategoryId = vm.CategoryId;

    await _db.SaveChangesAsync();
    return RedirectToAction(nameof(Details), new { slug = post.Slug });
}


        /* ---------- DELETE ---------- */
        [HttpPost("delete/{id:int}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _db.BlogPosts.FindAsync(id);
            if (post != null)
            {
                _db.BlogPosts.Remove(post);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /* ---------- HELPERS ---------- */
        private async Task<int> ResolveCategoryAsync(int selectedId, string? newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
            {
                var existing = await _db.Categories
                                        .FirstOrDefaultAsync(c => c.Name.ToLower() == newName.Trim().ToLower());
                if (existing != null) return existing.Id;

                var cat = new Category { Name = newName.Trim(), Slug = newName.Trim().ToLower() };
                _db.Categories.Add(cat);
                await _db.SaveChangesAsync();
                return cat.Id;
            }
            return selectedId;
        }

        private async Task<string?> SaveFileAsync(IFormFile? file, string subDir)
        {
            if (file == null || file.Length == 0) return null;

            var uploads = Path.Combine(_env.WebRootPath, "uploads", subDir);
            Directory.CreateDirectory(uploads);

            var name = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var path = Path.Combine(uploads, name);

            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/{subDir}/{name}";
        }
    }
}
