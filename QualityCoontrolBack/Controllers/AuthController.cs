using Microsoft.AspNetCore.Mvc;
using QualityControlAPI.Models;
using QualityControlAPI.Services.Auth;

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
            // 入参校验，防止空请求导致后续逻辑异常。
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("用户名和密码不能为空");
            }

            var user = await _authService.LoginAsync(request.Username, request.Password);
            if (user == null)
            {
                return Unauthorized("账号或密码错误");
            }

            return Ok(user);
        }

        [HttpPost("check-token")]
        public async Task<ActionResult<User>> CheckToken([FromBody] TokenCheckRequest request)
        {
            // 令牌校验接口需要确保 employeeId 和 token 都有效。
            if (request == null || string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("employeeId 和 token 不能为空");
            }

            var user = await _authService.ValidateTokenAsync(request.EmployeeId, request.Token);
            if (user == null)
            {
                return Unauthorized("登录已过期或已在其他地方登录");
            }
            return Ok(user);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersIdAndName()
        {
            var list = await _authService.GetAllUserIdAndNamesAsync();
            return Ok(list);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
