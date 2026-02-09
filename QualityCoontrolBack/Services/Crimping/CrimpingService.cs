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

        // --- 配置数据读取 ---
        public async Task<List<TerminalSpec>> GetTerminalsAsync() =>
            await _context.TerminalSpecs.AsNoTracking().ToListAsync();

        public async Task<List<WireSpec>> GetWiresAsync() =>
            await _context.WireSpecs.AsNoTracking().ToListAsync();

        public async Task<List<CrimpingTool>> GetToolsAsync() =>
            await _context.CrimpingTools.AsNoTracking().ToListAsync();

        public async Task<List<PullForceStandard>> GetStandardsAsync() =>
            await _context.PullForceStandards.AsNoTracking().ToListAsync();

        // --- 订单业务 ---

        public async Task<List<ProductionOrder>> GetOrdersAsync()
        {
            return await _context.Orders
                .Include(o => o.Records)
                    .ThenInclude(r => r.Samples)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        // 在 CrimpingService.cs 中修改此方法：
        public async Task<ProductionOrder> CreateOrderAsync(ProductionOrder order)
        {
            // 简单直接：直接保存，因为前端已经把所有数据（包括ID）都生成好了
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        // --- 检验记录业务 ---

        public async Task AddRecordAsync(string orderId, InspectionRecord record)
        {
            // 1. 检查订单是否存在
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) throw new KeyNotFoundException("未找到对应的生产订单");

            // 2. 关联订单 (现在类型匹配了，都是 string)
            record.OrderId = orderId;

            // 3. 保存
            _context.Records.Add(record);
            await _context.SaveChangesAsync();
        }

        public async Task AuditRecordAsync(string recordId, List<TerminalSample> samples, string auditorName, int status)
        {
            // recordId 现在是 string 类型
            var record = await _context.Records.FindAsync(recordId);
            if (record == null) throw new KeyNotFoundException("记录不存在");

            record.Status = status; // 0/1/2
            record.AuditorName = auditorName;
            record.AuditedAt = DateTime.Now;
            record.AuditNote = status == 1 ? "合格" : "不合格";

            // 更新关联的样本数据 (这里演示简单的覆盖逻辑：先删旧的，再加新的，或者直接更新数值)
            // 实际业务中，建议根据 SampleIndex 更新现有的 Sample 记录
            if (samples != null && samples.Any())
            {
                // 找出数据库里已有的样本
                var dbSamples = await _context.Samples
                    .Where(s => s.InspectionRecordId == recordId)
                    .ToListAsync();

                foreach (var sample in samples)
                {
                    var target = dbSamples.FirstOrDefault(s => s.SampleIndex == sample.SampleIndex);
                    if (target != null)
                    {
                        target.MeasuredForce = sample.MeasuredForce;
                        target.IsPassed = sample.IsPassed ?? false;
                    }
                    else
                    {
                        // 如果没找到，说明是新加的
                        sample.InspectionRecordId = recordId;
                        _context.Samples.Add(sample);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}