using Microsoft.AspNetCore.Identity;

namespace ChatApi.Models
{
    public class User : IdentityUser<int>
    {
        public DateTime? LastSeen { get; set; }
    }
}