using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using WisVestAPI.Models;
using WisVestAPI.Services;

namespace WisVestAPI.Controllers
{
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
private readonly UserService _userService;
private readonly PasswordHasher<User> _passwordHasher;
private readonly IConfiguration _configuration;

public AuthController(UserService userService, IConfiguration configuration)
{
_userService = userService;
_configuration = configuration;
_passwordHasher = new PasswordHasher<User>();
}

[HttpPost("register")]
public IActionResult Register([FromBody] UserRegisterRequest request)
{
if (_userService.UserExists(request.Email))
{
return BadRequest(new { message = "Email already exists" });
}

var user = new User
{
Email = request.Email
};

user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
_userService.AddUser(user); // No second hash

return Ok(new { message = "Registration successful" });
}

[HttpPost("login")]
public IActionResult Login([FromBody] LoginRequest request)
{
    Console.WriteLine($"Login attempt with email: {request.Email} , {request.Password}");
    var user = _userService.GetUserByEmail(request.Email);
    Console.WriteLine($"User found: {user}");
    if (user == null)
    {
        return Unauthorized(new { message = "Invalid credentials" });
    }

    var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (result == PasswordVerificationResult.Failed)
    {
        return Unauthorized(new { message = "Invalid credentials" });
    }

    var token = GenerateJwtToken(user);
    return Ok(new { message = "Login successful", token });
}

private string GenerateJwtToken(User user)
{
var secretKey = _configuration["JwtSettings:SecretKey"];
var issuer = _configuration["JwtSettings:Issuer"];
var audience = _configuration["JwtSettings:Audience"];

var claims = new[]
{
new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
new Claim(ClaimTypes.Name, user.Email)
};

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

var token = new JwtSecurityToken(
issuer: issuer,
audience: audience,
claims: claims,
expires: DateTime.UtcNow.AddHours(1),
signingCredentials: creds
);

return new JwtSecurityTokenHandler().WriteToken(token);
}

public class LoginRequest
{
public string Email { get; set; }
public string Password { get; set; }
}

public class UserRegisterRequest
{
public string Email { get; set; }
public string Password { get; set; }
}
}
}
