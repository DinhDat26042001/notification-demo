using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NotificationAPI.Models;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System;
using Microsoft.Data.SqlClient;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
namespace NotificationAPI.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

       
        [HttpPost("register")]
        public async Task<IActionResult> Register(User user)
        {
            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("MyDB")))
            {
                await conn.OpenAsync();

                // Kiểm tra xem Username đã tồn tại chưa
                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Username", user.Username);
                    int count = (int)await checkCmd.ExecuteScalarAsync();
                    if (count > 0)
                    {
                        return Conflict("Username already exists");
                    }
                }

                // Hash mật khẩu
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

                // Chèn dữ liệu mới
                string insertQuery = @"
            INSERT INTO Users (Username, PasswordHash, FullName, Email, CreatedAt, IsActive) 
            VALUES (@Username, @PasswordHash, @FullName, @Email, @CreatedAt, @IsActive)";

                using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@Username", user.Username);
                    insertCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                    insertCmd.Parameters.AddWithValue("@FullName", user.FullName);
                    insertCmd.Parameters.AddWithValue("@Email", user.Email);
                    insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                    insertCmd.Parameters.AddWithValue("@IsActive", true);

                    await insertCmd.ExecuteNonQueryAsync();
                }
            }

            return Ok("User registered successfully");
        }


        // --- Đăng nhập ---
        //[HttpPost("login")]
        //public async Task<IActionResult> Login(User loginRequest)
        //{

        //    if (loginRequest == null || string.IsNullOrEmpty(loginRequest.Username) || string.IsNullOrEmpty(loginRequest.PasswordHash))
        //    {
        //        return BadRequest("Username and password are required.");
        //    }
        //    Console.WriteLine($"Received Login Request: {loginRequest?.Username}, {loginRequest?.PasswordHash}");

        //    var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginRequest.Username);
        //    if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequest.PasswordHash, user.PasswordHash))
        //    {
        //        return Unauthorized("Invalid credentials");
        //    }

        //    var token = GenerateJwtToken(user);
        //    return Ok(new { token });
        //}
        [HttpPost("login")]
        public async Task<IActionResult> Login(User loginRequest)
        {
            if (loginRequest == null || string.IsNullOrEmpty(loginRequest.Username) || string.IsNullOrEmpty(loginRequest.PasswordHash))
            {
                return BadRequest("Username and password are required.");
            }

            User user = null;
            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("MyDB")))
            {
                await conn.OpenAsync();
                string query = "SELECT UserId, Username, PasswordHash FROM Users WHERE Username = @Username";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", loginRequest.Username);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            user = new User
                            {
                                UserId = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                PasswordHash = reader.GetString(2)
                            };
                        }
                    }
                }
            }

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequest.PasswordHash, user.PasswordHash))
            {
                return Unauthorized("Sai tài khoản hoặc mật khẩu rồi kìa");
            }
            // Cập nhật TokenNotification nếu có
            if (!string.IsNullOrEmpty(loginRequest.TokenNotification))
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("MyDB")))
                {
                    await conn.OpenAsync();
                    string updateQuery = "UPDATE NotiAPI_Users SET TokenNotification = @Token WHERE UserId = @UserId";
                    using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                    {
                        updateCmd.Parameters.AddWithValue("@Token", loginRequest.TokenNotification);
                        updateCmd.Parameters.AddWithValue("@UserId", user.UserId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            var token = GenerateJwtToken(user);
            return Ok(new
            {
                UserId = user.UserId,
                Token = token,
                UserName = user.Username
            });
        }

        [HttpPost("save-token")]
        public async Task<IActionResult> SaveExpoPushToken(User user)
        {
            if (user.UserId <= 0 || string.IsNullOrEmpty(user.TokenNotification))
                return BadRequest("Dữ liệu không hợp lệ");

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("MyDB")))
            {
                await conn.OpenAsync();
                string updateQuery = "UPDATE NotiAPI_Users SET TokenNotification = @Token WHERE UserId = @UserId";

                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Token", user.TokenNotification);
                    cmd.Parameters.AddWithValue("@UserId", user.UserId);

                    int rows = await cmd.ExecuteNonQueryAsync();
                    if (rows > 0)
                        return Ok("Token đã được lưu");
                }
            }
            return StatusCode(500, "Lỗi khi lưu token");
        }

        // --- Tạo JWT Token ---
        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
