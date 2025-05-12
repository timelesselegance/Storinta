using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HumanBodyWeb.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required, StringLength(60)]
        public string Name { get; set; } = default!;

        public string Slug { get; set; } = default!;

        // Çoklu ilişki için:
        public ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
    }
}
