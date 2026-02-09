using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Models;

namespace QualityControlAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // 将实体映射到数据库表
        public DbSet<User> Users { get; set; }
        public DbSet<CrimpingTool> CrimpingTools { get; set; }
        public DbSet<TerminalSpec> TerminalSpecs { get; set; }
        public DbSet<WireSpec> WireSpecs { get; set; }
        public DbSet<PullForceStandard> PullForceStandards { get; set; }
        public DbSet<ProductionOrder> Orders { get; set; }
        public DbSet<InspectionRecord> Records { get; set; }
        public DbSet<TerminalSample> Samples { get; set; }
    }
}