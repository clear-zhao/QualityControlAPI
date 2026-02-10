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
            // 返回包含新 Token 的用户信息
            return Ok(user);
        }

        // ✅ 新增：自动登录/状态检查接口
        [HttpPost("check-token")]
        public async Task<ActionResult<User>> CheckToken([FromBody] TokenCheckRequest request)
        {
            var user = await _authService.ValidateTokenAsync(request.EmployeeId, request.Token);
            if (user == null)
            {
                return Unauthorized("登录已过期或已在其他地方登录");
            }
            return Ok(user); // 验证成功，返回用户信息
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersIdAndName()
        {
            var list = await _authService.GetAllUserIdAndNamesAsync();
            return Ok(list);
        }
    }

    public class LoginRequest { public string Username { get; set; } public string Password { get; set; } }
}