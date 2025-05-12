using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace HumanBodyWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;
    }
}
