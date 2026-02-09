using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Data;
using QualityControlAPI.Models;

namespace QualityControlAPI.Services.Crimping
{
    public class CrimpingService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CrimpingService> _logger;

        public CrimpingService(AppDbContext context, ILogger<CrimpingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // --- 1. 查询 (Read) ---
        public async Task<List<ProductionOrder>> GetOrdersAsync()
        {
            return await _context.Orders
                .Include(o => o.Records)
                    .ThenInclude(r => r.Samples)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<ProductionOrder?> GetOrderByIdAsync(string id)
        {
            return await _context.Orders
                .Include(o => o.Records)
                    .ThenInclude(r => r.Samples)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        // --- 2. 新增 (Create) ---
        public async Task<ProductionOrder> CreateOrderAsync(ProductionOrder order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        // --- 3. 修改 (Update) ---
        public async Task UpdateOrderAsync(ProductionOrder order)
        {
            var existing = await _context.Orders
                .Include(o => o.Records)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (existing == null) throw new KeyNotFoundException("未找到该订单");

            // 业务逻辑限制：如果已有检验记录，禁止修改关键工艺参数（防止篡改追溯数据）
            if (existing.Records.Any())
            {
                // 只允许修改非核心字段，或者直接抛出异常
                // throw new InvalidOperationException("该订单已产生检验记录，无法修改工艺参数");
            }

            // 更新字段
            existing.ProductionOrderNo = order.ProductionOrderNo;
            existing.ProductName = order.ProductName;
            existing.ProductModel = order.ProductModel;
            existing.ToolNo = order.ToolNo;
            existing.TerminalSpecId = order.TerminalSpecId;
            existing.WireSpecId = order.WireSpecId;
            existing.StandardPullForce = order.StandardPullForce;

            await _context.SaveChangesAsync();
        }

        // --- 4. 删除 (Delete) ---
        public async Task DeleteOrderAsync(string id)
        {
            var order = await _context.Orders
                .Include(o => o.Records)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return;

            // 安全校验：如果订单包含已审核通过的记录，建议拦截删除
            if (order.Records.Any(r => r.Status == 1))
            {
                throw new InvalidOperationException("订单包含已合格的检验记录，不可删除");
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }

        // --- 5. 配置项获取 (保持不变) ---
        public async Task<List<TerminalSpec>> GetTerminalsAsync() => await _context.TerminalSpecs.AsNoTracking().ToListAsync();
        public async Task<List<WireSpec>> GetWiresAsync() => await _context.WireSpecs.AsNoTracking().ToListAsync();
        public async Task<List<CrimpingTool>> GetToolsAsync() => await _context.CrimpingTools.AsNoTracking().ToListAsync();
        public async Task<List<PullForceStandard>> GetStandardsAsync() => await _context.PullForceStandards.AsNoTracking().ToListAsync();

        // 1. 切换订单关闭状态
        public async Task ToggleOrderCloseStatusAsync(string id, bool isClosed)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) throw new KeyNotFoundException("订单不存在");

            order.IsClosed = isClosed;
            await _context.SaveChangesAsync();
        }

        // 2. 修改现有的 AddRecordAsync，增加逻辑保护
        public async Task AddRecordAsync(string orderId, InspectionRecord record)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) throw new KeyNotFoundException("未找到对应的生产订单");

            // 逻辑保护：已关闭的订单禁止提交检验
            if (order.IsClosed)
            {
                throw new InvalidOperationException("该订单已关闭，无法添加新的检验记录");
            }

            record.OrderId = orderId;
            _context.Records.Add(record);
            await _context.SaveChangesAsync();
        }


        public async Task AuditRecordAsync(string recordId, RecordAuditDto auditData)
        {
            // 1. 查找记录
            var record = await _context.Records
                .Include(r => r.Samples)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null) throw new KeyNotFoundException("未找到检验记录");

            // 2. 更新审核信息
            record.Status = auditData.Status; // 1=合格, 2=不合格
            record.AuditorName = auditData.AuditorName;
            record.AuditedAt = DateTime.Now;
            record.AuditNote = auditData.AuditNote;

            // 3. (可选) 如果审核时修改了样本实测值，同步更新
            if (auditData.Samples != null && auditData.Samples.Any())
            {
                foreach (var sampleUpdate in auditData.Samples)
                {
                    var existingSample = record.Samples.FirstOrDefault(s => s.SampleIndex == sampleUpdate.SampleIndex);
                    if (existingSample != null)
                    {
                        existingSample.MeasuredForce = sampleUpdate.MeasuredForce;
                        existingSample.IsPassed = sampleUpdate.IsPassed;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<ProductionOrder>> GetOrdersByCreatorEmployeeIdAsync(string employeeId, bool includeClosed = true)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                return new List<ProductionOrder>();

            var query = _context.Orders
                .Include(o => o.Records)
                    .ThenInclude(r => r.Samples)
                .AsNoTracking()
                .Where(o => o.CreatorEmployeeId != null && o.CreatorEmployeeId == employeeId);

            if (!includeClosed)
                query = query.Where(o => !o.IsClosed);

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        // --- 6. 删除检验记录 (Delete Record) ---
        public async Task DeleteRecordAsync(string recordId)
        {
            if (string.IsNullOrWhiteSpace(recordId))
                throw new ArgumentException("recordId 不能为空");

            var record = await _context.Records
                .Include(r => r.Samples)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null) return;

            // 如果你的数据库外键是 ON DELETE CASCADE，其实不 Include 也行；
            // 但 Include 后删除更直观，也兼容没建级联的情况（EF 会一起删 Samples）
            _context.Records.Remove(record);
            await _context.SaveChangesAsync();
        }



    }
}