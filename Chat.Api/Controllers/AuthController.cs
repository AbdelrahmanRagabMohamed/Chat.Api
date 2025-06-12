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
            // التحقق من صحة البيانات المدخلة
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new ApiResponse(400, "All fields are required"));
            }

            // التحقق من وجود الإيميل بطريقة صحيحة
            var emailExists = await CheckEmailExistsInternal(dto.Email);
            if (emailExists)
            {
                return BadRequest(new ApiResponse(400, "Email is already in use"));
            }

            // التحقق من وجود اسم المستخدم
            var existingUser = await _userManager.FindByNameAsync(dto.Username);
            if (existingUser != null)
            {
                return BadRequest(new ApiResponse(400, "Username is already taken"));
            }

            var user = new User
            {
                UserName = dto.Username,
                Email = dto.Email.ToLower(), // تأكد من تحويل الإيميل للأحرف الصغيرة
                EmailConfirmed = false, // يمكنك تعديل هذا حسب نظامك

            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new ApiResponse(400, errors));
            }

            return Ok(new { user.Id, user.UserName, user.Email });
        }

        // POST => baseUrl/api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new ApiResponse(400, "Username and password are required"));
            }

            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null)
            {
                return Unauthorized(new ApiResponse(401, "Invalid username or password"));
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
            {
                return Unauthorized(new ApiResponse(401, "Invalid username or password"));
            }

            var token = GenerateJwtToken(user);

            return Ok(new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName
            });
        }

        // Check if Email Exists - Public endpoint
        [HttpGet("emailExists")]
        public async Task<ActionResult<bool>> CheckEmailExists([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email parameter is required");

            var exists = await CheckEmailExistsInternal(email);
            return Ok(exists);
        }

        // Internal method for checking email existence
        private async Task<bool> CheckEmailExistsInternal(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.ToLower().Trim();

            // استخدام FirstOrDefaultAsync بدلاً من FindByEmailAsync لتجنب مشكلة العناصر المتعددة
            var user = await _userManager.Users
                .Where(u => u.Email.ToLower() == email)
                .FirstOrDefaultAsync();

            return user != null;
        }

        // Generate JWT Token Function
        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key is not configured");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1), // استخدم UTC
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}