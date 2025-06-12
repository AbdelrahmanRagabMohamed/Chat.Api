using Microsoft.AspNetCore.Identity;

namespace ChatApi.Models
{
    public class User : IdentityUser<int>
    {
        public string Email { get; set; }
        public string? Image { get; set; } // Image URL
    }
}