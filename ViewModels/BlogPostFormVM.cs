using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using HumanBodyWeb.Models;

namespace HumanBodyWeb.ViewModels
{
    public class BlogPostFormVM
    {
        /* --- form alanları --- */
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = "";

        [Required, StringLength(150)]
        public string Slug { get; set; } = "";

        [Required]
        public string Content { get; set; } = "";

        public int CategoryId { get; set; }          // combobox seçimi
        public string? NewCategoryName { get; set; } // “yeni kategori” input

        public IFormFile? FeaturedImageFile { get; set; }
        public string?     FeaturedImageUrl  { get; set; }

        public bool   IsDraft     { get; set; }
        public DateTime? PublishedOn { get; set; }

        /* --- yardımcı --- */
        public IEnumerable<Category> Categories { get; set; } = Array.Empty<Category>();

        /* Var olan blog post → VM dönüşümü (Edit GET) */
        public static BlogPostFormVM FromEntity(BlogPost p) => new()
        {
            Id              = p.Id,
            Title           = p.Title,
            Slug            = p.Slug,
            Content         = p.Content,
            CategoryId      = p.CategoryId,
            FeaturedImageUrl= p.FeaturedImageUrl,
            IsDraft         = p.IsDraft,
            PublishedOn     = p.PublishedOn
        };
    }
}
