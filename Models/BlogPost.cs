using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace HumanBodyWeb.Models
{
    public class BlogPost
    {
        public int Id { get; set; }
        public string Title   { get; set; } = null!;
        public string Slug    { get; set; } = null!;
        public string Content { get; set; } = null!;

        public string? FeaturedImageUrl { get; set; }

        [NotMapped]
        public IFormFile? FeaturedImageFile { get; set; }
        public DateTime? PublishedOn { get; set; } = DateTime.UtcNow;
        public bool IsDraft { get; set; } 
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }
}
