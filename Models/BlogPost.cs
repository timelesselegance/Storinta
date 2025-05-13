using System;
using System.ComponentModel.DataAnnotations; // [Required] gibi attribute'lar için
using System.ComponentModel.DataAnnotations.Schema; // [NotMapped] attribute'u için
using Microsoft.AspNetCore.Http; // IFormFile için

namespace HumanBodyWeb.Models // Namespace'inizin doğru olduğundan emin olun
{
    public class BlogPost
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Başlık alanı gereklidir.")]
        [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
        public string Title { get; set; } = null!; // null!; EF Core tarafından atanacağını varsayar

        [Required(ErrorMessage = "Slug alanı gereklidir.")]
        [StringLength(250, ErrorMessage = "Slug en fazla 250 karakter olabilir.")]
        public string Slug { get; set; } = null!;

        [Required(ErrorMessage = "İçerik alanı gereklidir.")]
        public string Content { get; set; } = null!;

        public string? FeaturedImageUrl { get; set; } // Cloudinary URL'si için

        // --- DÜZELTME: public erişim belirleyicisi eklendi ---
        public string? FeaturedImagePublicId { get; set; } // Cloudinary Public ID'si için

        [NotMapped] // Bu alan veritabanına kaydedilmeyecek
        public IFormFile? FeaturedImageFile { get; set; } // Görsel yükleme formu için

        public bool IsDraft { get; set; }

        // --- YENİ EKLENEN ÖZELLİKLER ---
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; } // Null olabilir, çünkü her yazı hemen güncellenmeyebilir
        // --- ---

        public DateTime? PublishedOn { get; set; } // Varsayılan olarak null bırakmak daha iyi olabilir,
                                                   // ve yayınlandığında controller'da set edilebilir.
                                                   // Ya da: = null;

        [Required(ErrorMessage = "Kategori seçimi gereklidir.")]
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!; // Navigation property

        // Constructor (İsteğe bağlı: bazı varsayılan değerleri atamak için)
        public BlogPost()
        {
            CreatedOn = DateTime.UtcNow; // Yeni bir BlogPost oluşturulduğunda CreatedOn otomatik atansın
            // PublishedOn = IsDraft ? null : DateTime.UtcNow; // Bu tür bir mantık da eklenebilir
        }
    }
}