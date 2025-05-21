using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HumanBodyWeb.Controllers
{
    public class FileController : Controller
    {
        private readonly IWebHostEnvironment _env;
        public FileController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // GET /File/GetImages
        [HttpGet]
        public IActionResult GetImages()
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var images = Directory.GetFiles(folder)
                .Select(f => new {
                    url   = Url.Content("~/uploads/" + Path.GetFileName(f)),
                    title = Path.GetFileName(f)
                })
                .ToList();

            return Json(images);
        }

        // POST /File/UploadImage
        [HttpPost]
        [RequestSizeLimit(10_000_000)]  // Maksimum 10MB
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Dosya bulunamadı.");

            var folder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            // Benzersiz isim üret
            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // TinyMCE ve galerinin beklediği format
            return Json(new {
                url   = Url.Content("~/uploads/" + fileName),
                title = fileName
            });
        }
    }
}
