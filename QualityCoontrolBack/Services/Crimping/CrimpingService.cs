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
            // 安全校验：关键标识为空时拒绝创建，避免脏数据进入数据库
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (string.IsNullOrWhiteSpace(order.Id) || string.IsNullOrWhiteSpace(order.ProductionOrderNo))
                throw new InvalidOperationException("订单ID和生产工单号不能为空");

            var exists = await _context.Orders.AnyAsync(o => o.Id == order.Id);
            if (exists)
                throw new InvalidOperationException("订单ID已存在");

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task UpdateOrderAsync(ProductionOrder order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (string.IsNullOrWhiteSpace(order.Id) || string.IsNullOrWhiteSpace(order.ProductionOrderNo))
                throw new InvalidOperationException("订单ID和生产工单号不能为空");

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
            if (string.IsNullOrWhiteSpace(orderId))
                throw new ArgumentException("orderId 不能为空");

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            if (string.IsNullOrWhiteSpace(record.Id))
                throw new InvalidOperationException("检验记录ID不能为空");

            var duplicateRecord = await _context.Records.AnyAsync(r => r.Id == record.Id);
            if (duplicateRecord)
                throw new InvalidOperationException("检验记录ID已存在");

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
            if (string.IsNullOrWhiteSpace(recordId))
                throw new ArgumentException("recordId 不能为空");

            if (auditData == null)
                throw new ArgumentNullException(nameof(auditData));

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

        public async Task<List<TerminalSpecResponseDto>> GetTerminalsAsync()
        {
            // 1) 先查启用的端子规格，保留 method 编号用于后续标准匹配
            var terminals = await _context.TerminalSpecs
                .AsNoTracking()
                .Where(t => !t.IsDisabled)
                .ToListAsync();

            if (!terminals.Any())
                return new List<TerminalSpecResponseDto>();

            // 2) 用 method 编号关联字典表，拿到方法中文名
            var methodCodes = terminals.Select(t => t.Method).Distinct().ToList();
            var methodNameMap = await _context.CrimpingMethodDicts
                .AsNoTracking()
                .Where(d => !d.IsDisabled && methodCodes.Contains(d.Code))
                .ToDictionaryAsync(d => d.Code, d => d.Name);

            // 3) 组装返回 DTO；若字典缺失则给兜底文案，避免前端空值
            return terminals.Select(t => new TerminalSpecResponseDto
            {
                Id = t.Id,
                MaterialCode = t.MaterialCode,
                Name = t.Name,
                Description = t.Description,
                Method = t.Method,
                MethodName = methodNameMap.TryGetValue(t.Method, out var methodName) ? methodName : $"方法{t.Method}",
                IsDisabled = t.IsDisabled
            }).ToList();
        }

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
