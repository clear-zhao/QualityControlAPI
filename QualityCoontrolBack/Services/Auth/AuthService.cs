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

        // ✅ 新增：获取所有用户（Id + Name）
        public async Task<List<UserNameDto>> GetAllUserIdAndNamesAsync()
        {
            return await _context.Users
                .AsNoTracking()
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
            // 1. 验证账号密码
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeId == username && u.Password == password);

            if (user != null)
            {
                // 2. 登录成功，生成新 Token（实现“后登顶掉前登”）
                user.Token = Guid.NewGuid().ToString("N");
                // 设置有效期，例如 7 天
                user.TokenExpireTime = DateTime.Now.AddDays(7);

                await _context.SaveChangesAsync();
            }
            return user;
        }

        // 新增：通过 Token 验证用户是否依然合法在线
        public async Task<User?> ValidateTokenAsync(string employeeId, string token)
        {
            return await _context.Users
                .AsNoTracking() // 仅查询，不需要追踪
                .FirstOrDefaultAsync(u => u.EmployeeId == employeeId
                                       && u.Token == token
                                       && u.TokenExpireTime > DateTime.Now);
        }
    }
}
