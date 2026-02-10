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

        // =========================================================
        // 订单查询 (Read)
        // =========================================================

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

        // =========================================================
        // 订单新增 / 修改 / 删除 (Create / Update / Delete)
        // =========================================================

        public async Task<ProductionOrder> CreateOrderAsync(ProductionOrder order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task UpdateOrderAsync(ProductionOrder order)
        {
            var existing = await _context.Orders
                .Include(o => o.Records)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (existing == null)
                throw new KeyNotFoundException("未找到该订单");

            // 业务逻辑限制：如果已有检验记录，禁止修改关键工艺参数（防止篡改追溯数据）
            if (existing.Records.Any())
            {
                // 你可以在这里更严格：
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

        public async Task DeleteOrderAsync(string id)
        {
            var order = await _context.Orders
                .Include(o => o.Records)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return;

            // 安全校验：如果订单包含已审核通过的记录，建议拦截删除
            if (order.Records.Any(r => r.Status == 1))
                throw new InvalidOperationException("订单包含已合格的检验记录，不可删除");

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }

        // =========================================================
        // 订单状态控制 (Close / Reopen)
        // =========================================================

        public async Task ToggleOrderCloseStatusAsync(string id, bool isClosed)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                throw new KeyNotFoundException("订单不存在");

            order.IsClosed = isClosed;
            await _context.SaveChangesAsync();
        }

        // 修改订单的工具编号 ToolNo
        public async Task UpdateOrderToolNoAsync(string orderId, string? toolNo)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                throw new ArgumentException("orderId 不能为空");

            // 如果你不允许传空，就打开这段
            if (string.IsNullOrWhiteSpace(toolNo))
                throw new InvalidOperationException("工具编号不能为空");

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException("订单不存在");

            // 可选：若你希望订单关闭后不允许改工具
            // if (order.IsClosed)
            //     throw new InvalidOperationException("订单已关闭，禁止修改工具编号");

            order.ToolNo = toolNo;
            await _context.SaveChangesAsync();
        }


        // =========================================================
        // 检验记录：新增 / 审核 / 删除 (Record CRUD + Audit)
        // =========================================================

        public async Task AddRecordAsync(string orderId, InspectionRecord record)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException("未找到对应的生产订单");

            if (order.IsClosed)
                throw new InvalidOperationException("该订单已关闭，无法添加新的检验记录");

            // 只绑定外键
            record.OrderId = orderId;

            // ✅ 不做任何“从订单同步工具编号”的行为
            // record.InspectionToolNo 由前端提交（每条记录独立）

            _context.Records.Add(record);
            await _context.SaveChangesAsync();
        }


        public async Task AuditRecordAsync(string recordId, RecordAuditDto auditData)
        {
            var record = await _context.Records
                .Include(r => r.Samples)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null)
                throw new KeyNotFoundException("未找到检验记录");

            record.Status = auditData.Status; // 1=合格, 2=不合格
            record.AuditorName = auditData.AuditorName;
            record.AuditedAt = DateTime.Now;
            record.AuditNote = auditData.AuditNote;

            if (auditData.Samples != null && auditData.Samples.Any())
            {
                foreach (var sampleUpdate in auditData.Samples)
                {
                    var existingSample = record.Samples
                        .FirstOrDefault(s => s.SampleIndex == sampleUpdate.SampleIndex);

                    if (existingSample != null)
                    {
                        existingSample.MeasuredForce = sampleUpdate.MeasuredForce;
                        existingSample.IsPassed = sampleUpdate.IsPassed;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteRecordAsync(string recordId)
        {
            if (string.IsNullOrWhiteSpace(recordId))
                throw new ArgumentException("recordId 不能为空");

            var record = await _context.Records
                .Include(r => r.Samples)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null) return;

            _context.Records.Remove(record);
            await _context.SaveChangesAsync();
        }

        // =========================================================
        // 基础配置数据获取 (下拉选项等)
        // =========================================================

        public async Task<List<TerminalSpec>> GetTerminalsAsync()
            => await _context.TerminalSpecs.AsNoTracking().ToListAsync();

        public async Task<List<WireSpec>> GetWiresAsync()
            => await _context.WireSpecs.AsNoTracking().ToListAsync();

        public async Task<List<CrimpingTool>> GetToolsAsync()
            => await _context.CrimpingTools.AsNoTracking().ToListAsync();

        public async Task<List<PullForceStandard>> GetStandardsAsync()
            => await _context.PullForceStandards.AsNoTracking().ToListAsync();
    }
}
