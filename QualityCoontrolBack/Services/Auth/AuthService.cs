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

        // ✅ 获取所有未被禁用的用户（Id + Name）
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
            // 安全校验：空账号或空密码直接返回，避免无效数据库查询
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            // 1. 查用户（先按账号查，密码在内存中做安全校验，支持哈希）
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeId == username
                                       && !u.IsDisabled); // 【修改点2】：禁止被禁用的账号登录

            if (user == null)
                return null;

            var isPasswordValid = PasswordHasher.VerifyPassword(password, user.Password);
            if (!isPasswordValid)
                return null;

            // 2. 老数据平滑迁移：如果当前是明文密码，登录成功后自动升级为哈希存储
            if (!PasswordHasher.IsHashed(user.Password))
            {
                user.Password = PasswordHasher.HashPassword(password);
            }

            // 3. 登录成功，生成新 Token（实现“后登顶掉前登”）
            user.Token = Guid.NewGuid().ToString("N");
            // 设置有效期，例如 7 天
            user.TokenExpireTime = DateTime.Now.AddDays(7);

            await _context.SaveChangesAsync();
            return user;
        }

        // 通过 Token 验证用户是否依然合法在线
        public async Task<User?> ValidateTokenAsync(string employeeId, string token)
        {
            // 安全校验：空参数直接返回，避免放行异常请求
            if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(token))
                return null;

            return await _context.Users
                .AsNoTracking() // 仅查询，不需要追踪
                .FirstOrDefaultAsync(u => u.EmployeeId == employeeId
                                       && u.Token == token
                                       && u.TokenExpireTime > DateTime.Now
                                       && !u.IsDisabled); // 【修改点3】：如果在线期间被管理员禁用，验证将失败，强制顶下线
        }
    }
}
