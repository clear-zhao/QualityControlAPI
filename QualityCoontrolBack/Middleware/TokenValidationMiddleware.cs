using System.Text.Json;
using QualityControlAPI.Services.Auth;

namespace QualityControlAPI.Middleware
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AuthService authService)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (IsAnonymousPath(path))
            {
                await _next(context);
                return;
            }

            var employeeId = context.Request.Headers["X-Employee-Id"].FirstOrDefault();
            var token = context.Request.Headers["X-Token"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(token))
            {
                await WriteUnauthorizedAsync(context, "拒绝操作：缺少登录凭证");
                return;
            }

            var user = await authService.ValidateTokenAsync(employeeId, token);
            if (user == null)
            {
                await WriteUnauthorizedAsync(context, "拒绝操作：账户已在别处登录或登录已失效");
                return;
            }

            await _next(context);
        }

        // 匿名白名单：无需 Token 即可访问的路径，新增接口只需追加一行
        private static readonly string[] AnonymousPaths =
        [
            "/api/Auth/login",
            "/api/Auth/check-token",
            "/api/Auth/users",       // 登录页需要在认证前获取员工列表
        ];

        private static bool IsAnonymousPath(string path)
        {
            // Swagger 路径按前缀匹配，业务路径精确匹配
            if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
                return true;

            return AnonymousPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
        }
    }
}
