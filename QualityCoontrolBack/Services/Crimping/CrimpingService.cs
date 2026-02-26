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
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return await _context.Orders
                .Include(o => o.Records)
                    .ThenInclude(r => r.Samples)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id.Trim());
        }

        public async Task<List<ProductionOrder>> GetOrdersByCreatorEmployeeIdAsync(string employeeId, bool includeClosed = true)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                return new List<ProductionOrder>();

            var safeEmployeeId = employeeId.Trim();

            var query = _context.Orders
                .Include(o => o.Records)
                    .ThenInclude(r => r.Samples)
                .AsNoTracking()
                .Where(o => o.CreatorEmployeeId != null && o.CreatorEmployeeId == safeEmployeeId);

            if (!includeClosed)
                query = query.Where(o => !o.IsClosed);

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProductionOrder> CreateOrderAsync(ProductionOrder order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (string.IsNullOrWhiteSpace(order.Id) || string.IsNullOrWhiteSpace(order.ProductionOrderNo))
                throw new ArgumentException("订单 Id 和 ProductionOrderNo 不能为空");

            order.Id = order.Id.Trim();
            order.ProductionOrderNo = order.ProductionOrderNo.Trim();

            var exists = await _context.Orders.AnyAsync(o => o.Id == order.Id);
            if (exists)
                throw new InvalidOperationException($"订单ID '{order.Id}' 已存在");

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task UpdateOrderAsync(ProductionOrder order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (string.IsNullOrWhiteSpace(order.Id))
                throw new ArgumentException("订单 Id 不能为空");

            var orderId = order.Id.Trim();
            var existing = await _context.Orders
                .Include(o => o.Records)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (existing == null)
                throw new KeyNotFoundException("未找到该订单");

            // 业务逻辑限制：如果已有检验记录，必要时可禁止修改关键字段。
            if (existing.Records.Any())
            {
                _logger.LogInformation("Order {OrderId} already has records, updating with caution", orderId);
            }

            existing.ProductionOrderNo = order.ProductionOrderNo?.Trim() ?? existing.ProductionOrderNo;
            existing.ProductName = order.ProductName?.Trim();
            existing.ProductModel = order.ProductModel?.Trim();
            existing.ToolNo = order.ToolNo?.Trim();
            existing.TerminalSpecId = order.TerminalSpecId?.Trim();
            existing.WireSpecId = order.WireSpecId?.Trim();
            existing.StandardPullForce = order.StandardPullForce;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteOrderAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("订单 Id 不能为空");

            var orderId = id.Trim();

            var order = await _context.Orders
                .Include(o => o.Records)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return;

            if (order.Records.Any(r => r.Status == 1))
                throw new InvalidOperationException("订单包含已合格的检验记录，不可删除");

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleOrderCloseStatusAsync(string id, bool isClosed)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("订单 Id 不能为空");

            var order = await _context.Orders.FindAsync(id.Trim());
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

            var order = await _context.Orders.FindAsync(orderId.Trim());
            if (order == null)
                throw new KeyNotFoundException("订单不存在");

            order.ToolNo = toolNo.Trim();
            await _context.SaveChangesAsync();
        }

        public async Task AddRecordAsync(string orderId, InspectionRecord record)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                throw new ArgumentException("orderId 不能为空");

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            if (string.IsNullOrWhiteSpace(record.Id))
                throw new ArgumentException("检验记录 Id 不能为空");

            var safeOrderId = orderId.Trim();
            record.Id = record.Id.Trim();

            var order = await _context.Orders.FindAsync(safeOrderId);
            if (order == null)
                throw new KeyNotFoundException("未找到对应的生产订单");

            if (order.IsClosed)
                throw new InvalidOperationException("该订单已关闭，无法添加新的检验记录");

            var exists = await _context.Records.AnyAsync(r => r.Id == record.Id);
            if (exists)
                throw new InvalidOperationException($"检验记录ID '{record.Id}' 已存在");

            record.OrderId = safeOrderId;

            _context.Records.Add(record);
            await _context.SaveChangesAsync();
        }

        public async Task AuditRecordAsync(string recordId, RecordAuditDto auditData)
        {
            if (string.IsNullOrWhiteSpace(recordId))
                throw new ArgumentException("recordId 不能为空");

            if (auditData == null)
                throw new ArgumentNullException(nameof(auditData));

            var record = await _context.Records
                .Include(r => r.Samples)
                .FirstOrDefaultAsync(r => r.Id == recordId.Trim());

            if (record == null)
                throw new KeyNotFoundException("未找到检验记录");

            record.Status = auditData.Status;
            record.AuditorName = auditData.AuditorName?.Trim();
            record.AuditedAt = DateTime.Now;
            record.AuditNote = auditData.AuditNote?.Trim();

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
                .FirstOrDefaultAsync(r => r.Id == recordId.Trim());

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
