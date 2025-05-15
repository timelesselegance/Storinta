using System;
using System.ComponentModel.DataAnnotations; // [Required] gibi attribute'lar için
using System.ComponentModel.DataAnnotations.Schema; // [NotMapped] için
using Microsoft.AspNetCore.Http; // IFormFile için

namespace HumanBodyWeb.Models
{
    public class BlogPost
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Başlık alanı gereklidir.")]
        [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Slug alanı gereklidir.")]
        [StringLength(250, ErrorMessage = "Slug en fazla 250 karakter olabilir.")]
        public string Slug { get; set; } = null!;

        [Required(ErrorMessage = "İçerik alanı gereklidir.")]
        public string Content { get; set; } = null!;

        public string? FeaturedImageUrl { get; set; }
        public string? FeaturedImagePublicId { get; set; }

        [NotMapped]
        public IFormFile? FeaturedImageFile { get; set; }

        [Required]
        public bool IsDraft { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public DateTime? PublishedOn { get; set; }

        [Required(ErrorMessage = "Kategori seçimi gereklidir.")]
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!;

        // Yeni eklenen yazar bilgisi
        [Required]
        public string AuthorId { get; set; } = null!;
        public virtual ApplicationUser Author { get; set; } = null!;
        public int ViewCount { get; set; } = 0;

        public BlogPost()
        {
            CreatedOn = DateTime.UtcNow;
        }
    }
}