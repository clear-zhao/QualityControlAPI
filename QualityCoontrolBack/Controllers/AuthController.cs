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
        public async Task<ActionResult<UserResponseDto>> Login([FromBody] LoginRequest request)
        {
            // 安全校验：避免空请求或空凭据导致异常查询
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("账号和密码不能为空");
            }

            var user = await _authService.LoginAsync(request.Username, request.Password);
            if (user == null)
            {
                return Unauthorized("账号或密码错误");
            }
            // 返回 DTO 而非实体，避免泄露密码等敏感字段
            return Ok(MapToDto(user));
        }

        // 自动登录/状态检查接口
        [HttpPost("check-token")]
        public async Task<ActionResult<UserResponseDto>> CheckToken([FromBody] TokenCheckRequest request)
        {
            // 安全校验：避免空参数通过后触发无效鉴权
            if (request == null || string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("employeeId 和 token 不能为空");
            }

            var user = await _authService.ValidateTokenAsync(request.EmployeeId, request.Token);
            if (user == null)
            {
                return Unauthorized("登录已过期或已在其他地方登录");
            }
            return Ok(MapToDto(user));
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersIdAndName()
        {
            var list = await _authService.GetAllUserIdAndNamesAsync();
            return Ok(list);
        }

        // 将 User 实体映射为 DTO，统一脱敏处理
        private static UserResponseDto MapToDto(User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            EmployeeId = user.EmployeeId,
            Role = user.Role,
            IsDisabled = user.IsDisabled,
            Token = user.Token,
            TokenExpireTime = user.TokenExpireTime,
        };
    }

    public class LoginRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }
}
