using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HumanBodyWeb.Models
{
    public class PostUserView
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PostId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        public DateTime ViewedOn { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(PostId))]
        public virtual BlogPost Post { get; set; } = null!;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; } = null!;
    }
}
