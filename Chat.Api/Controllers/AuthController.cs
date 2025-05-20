using Chat.Api.DTOs;
using ChatApi.Errors;
using ChatApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ChatApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }


        // POST => baseUrl/api/auth/register
        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] RegisterDto dto)
        {
            // Works Synchronous [Stop all until Check]
            if (CheckEmailExists(dto.Email).Result.Value)
            {
                return BadRequest(new ApiResponse(400, "Email is already in use"));
            }

            var user = new User
            {
                UserName = dto.Username,
                Email = dto.Email
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { user.Id, user.UserName });
        }


        // POST => baseUrl/api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null)
                return Unauthorized("Invalid username or password");

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
                return Unauthorized("Invalid username or password");

            var token = GenerateJwtToken(user);

            return Ok(new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName
            });
        }


        // Check if Email Exsits
        // GET => baseUrl/api/auth/emailExsits
        [HttpGet("emailExists")]
        public async Task<ActionResult<bool>> CheckEmailExists([FromQuery] string email)
        {
            // التحقق من الإيميل
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email parameter is required");

            // تحويل الإيميل للحروف الصغيرة
            email = email.ToLower();

            // البحث عن أي مستخدم بنفس الإيميل
            var users = await _userManager.Users
                .Where(u => u.Email == email)
                .ToListAsync();

            return users.Any();
        }

        //---------------------------------------------------//

        // Generate JWT Token Function
        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}