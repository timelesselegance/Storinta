using System.Collections.Generic;
using HumanBodyWeb.Models;

namespace HumanBodyWeb.ViewModels
{
    public class BlogListViewModel
    {
        public string? SearchQuery { get; set; }
        public int? SelectedCategoryId { get; set; }

        public List<BlogPost> Posts { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
    }
}
