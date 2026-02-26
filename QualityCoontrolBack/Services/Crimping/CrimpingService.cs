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
            if (string.IsNullOrWhiteSpace(id)) return null;

            return await _context.Orders
                .Include(o => o.Records)
                    .ThenInclude(r => r.Samples)
                .AsNoTracking()
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
                .Where(o => o.CreatorEmployeeId == employeeId);

            if (!includeClosed)
                query = query.Where(o => !o.IsClosed);

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProductionOrder> CreateOrderAsync(ProductionOrder order)
        {
            if (string.IsNullOrWhiteSpace(order.Id) || string.IsNullOrWhiteSpace(order.ProductionOrderNo))
                throw new InvalidOperationException("订单ID和生产单号不能为空");

            // 防止重复主键导致数据库异常，提前做业务校验并返回友好信息。
            var existed = await _context.Orders.AnyAsync(o => o.Id == order.Id);
            if (existed)
                throw new InvalidOperationException("订单ID已存在，请勿重复创建");

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

            if (order.Records.Any(r => r.Status == 1))
                throw new InvalidOperationException("订单包含已合格的检验记录，不可删除");

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleOrderCloseStatusAsync(string id, bool isClosed)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                throw new KeyNotFoundException("订单不存在");

            order.IsClosed = isClosed;
            await _context.SaveChangesAsync();
        }

        public async Task UpdateOrderToolNoAsync(string orderId, string? toolNo)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                throw new ArgumentException("orderId 不能为空");

            if (string.IsNullOrWhiteSpace(toolNo))
                throw new InvalidOperationException("工具编号不能为空");

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException("订单不存在");

            order.ToolNo = toolNo;
            await _context.SaveChangesAsync();
        }

        public async Task AddRecordAsync(string orderId, InspectionRecord record)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                throw new ArgumentException("orderId 不能为空");
            if (string.IsNullOrWhiteSpace(record.Id))
                throw new InvalidOperationException("记录ID不能为空");

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException("未找到对应的生产订单");

            if (order.IsClosed)
                throw new InvalidOperationException("该订单已关闭，无法添加新的检验记录");

            var existed = await _context.Records.AnyAsync(r => r.Id == record.Id);
            if (existed)
                throw new InvalidOperationException("检验记录ID已存在，请勿重复提交");

            record.OrderId = orderId;
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

            record.Status = auditData.Status;
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

        public async Task<List<TerminalSpec>> GetTerminalsAsync()
            => await _context.TerminalSpecs
                .AsNoTracking()
                .Where(t => !t.IsDisabled)
                .ToListAsync();

        public async Task<List<WireSpec>> GetWiresAsync()
            => await _context.WireSpecs
                .AsNoTracking()
                .Where(w => !w.IsDisabled)
                .ToListAsync();

        public async Task<List<CrimpingTool>> GetToolsAsync()
            => await _context.CrimpingTools
                .AsNoTracking()
                .Where(c => !c.IsDisabled)
                .ToListAsync();

        public async Task<List<PullForceStandard>> GetStandardsAsync()
            => await _context.PullForceStandards
                .AsNoTracking()
                .Where(s => !s.IsDisabled)
                .ToListAsync();
    }
}
