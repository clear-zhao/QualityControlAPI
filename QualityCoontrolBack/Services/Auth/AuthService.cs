using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Data;
using QualityControlAPI.Models;

namespace QualityControlAPI.Services.Auth
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        // 获取所有未被禁用的用户（Id + Name）
        public async Task<List<UserNameDto>> GetAllUserIdAndNamesAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDisabled)
                .OrderBy(u => u.Name)
                .Select(u => new UserNameDto
                {
                    Id = u.EmployeeId,
                    Name = u.Name
                })
                .ToListAsync();
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            // 防御性校验：避免空参数导致无意义查询。
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var safeUsername = username.Trim();
            var safePassword = password.Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeId == safeUsername
                                       && u.Password == safePassword
                                       && !u.IsDisabled);

            if (user != null)
            {
                // 登录成功时刷新 token，实现“后登顶掉前登”。
                user.Token = Guid.NewGuid().ToString("N");
                user.TokenExpireTime = DateTime.Now.AddDays(7);

                await _context.SaveChangesAsync();
            }
            return user;
        }

        // 通过 Token 验证用户是否依然在线。
        public async Task<User?> ValidateTokenAsync(string employeeId, string token)
        {
            if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var safeEmployeeId = employeeId.Trim();
            var safeToken = token.Trim();

            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.EmployeeId == safeEmployeeId
                                       && u.Token == safeToken
                                       && u.TokenExpireTime > DateTime.Now
                                       && !u.IsDisabled);
        }
    }
}
