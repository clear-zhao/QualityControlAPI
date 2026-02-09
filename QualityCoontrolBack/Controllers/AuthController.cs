using Microsoft.AspNetCore.Mvc;
using QualityControlAPI.Services.Auth;
using QualityControlAPI.Models;

namespace QualityControlAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<User>> Login([FromBody] LoginRequest request)
        {
            var user = await _authService.LoginAsync(request.Username, request.Password);
            if (user == null)
            {
                return Unauthorized("账号或密码错误");
            }
            return Ok(user);
        }
        // ✅ GET: /api/auth/users
        // 返回：[{ "id": 1, "name": "管理员" }, ...]
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersIdAndName()
        {
            var list = await _authService.GetAllUserIdAndNamesAsync();
            return Ok(list);
        }
    }

    public class LoginRequest { public string Username { get; set; } public string Password { get; set; } }
}