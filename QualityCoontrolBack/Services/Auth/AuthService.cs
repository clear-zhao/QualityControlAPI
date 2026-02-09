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

        public async Task<User?> LoginAsync(string username, string password)
        {
            // 使用 EmployeeId 作为登录账号
            return await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeId == username && u.Password == password);
        }
    }
}