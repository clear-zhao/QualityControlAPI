using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Models;
using QualityControlAPI.Services.Crimping;

namespace QualityControlAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly CrimpingService _service;

        public OrdersController(CrimpingService service)
        {
            _service = service;
        }

        // =========================================================
        // 订单查询 (Read)
        // =========================================================

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductionOrder>>> GetAll()
            => Ok(await _service.GetOrdersAsync());

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductionOrder>> GetById(string id)
        {
            var order = await _service.GetOrderByIdAsync(id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        [HttpGet("orders/by-creator-employee")]
        public async Task<IActionResult> GetOrdersByCreatorEmployeeId(
            [FromQuery] string employeeId,
            [FromQuery] bool includeClosed = true)
        {
            var list = await _service.GetOrdersByCreatorEmployeeIdAsync(employeeId, includeClosed);
            return Ok(list);
        }

        // =========================================================
        // 订单新增 / 修改 / 删除 (Create / Update / Delete)
        // =========================================================

        [HttpPost]
        public async Task<ActionResult<ProductionOrder>> Create([FromBody] ProductionOrder order)
        {
            // 安全校验：提前拦截空请求体，避免服务层空引用
            if (order == null) return BadRequest("订单数据不能为空");

            try
            {
                var result = await _service.CreateOrderAsync(order);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateException)
            {
                // 数据库写入失败时可重试，避免前端误判为业务错误
                return StatusCode(503, "数据库繁忙或写入失败，请稍后重试");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ProductionOrder order)
        {
            if (order == null) return BadRequest("订单数据不能为空");
            if (id != order.Id) return BadRequest("请求ID不一致");

            try
            {
                await _service.UpdateOrderAsync(order);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                // 并发更新冲突时返回 409，提示客户端重新拉取后再提交
                return Conflict("数据已被其他用户修改，请刷新后重试");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _service.DeleteOrderAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // =========================================================
        // 订单状态控制 (Close / Reopen)
        // =========================================================

        [HttpPatch("{id}/close")]
        public async Task<IActionResult> CloseOrder(string id, [FromBody] bool isClosed)
        {
            try
            {
                await _service.ToggleOrderCloseStatusAsync(id, isClosed);
                return Ok(new { message = isClosed ? "订单已关闭" : "订单已重新激活" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PATCH: api/orders/{id}/tool
        [HttpPatch("{id}/tool")]
        public async Task<IActionResult> UpdateOrderTool(string id, [FromBody] UpdateOrderToolDto dto)
        {
            if (dto == null) return BadRequest("请求体不能为空");

            try
            {
                await _service.UpdateOrderToolNoAsync(id, dto.ToolNo);
                return Ok(new { message = "订单工具编号已更新", orderId = id, toolNo = dto.ToolNo });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"服务器错误: {ex.Message}");
            }
        }


        // =========================================================
        // 检验记录：新增 / 审核 / 删除 (Record CRUD + Audit)
        // =========================================================

        [HttpPost("{orderId}/records")]
        public async Task<IActionResult> AddRecord(string orderId, [FromBody] InspectionRecord record)
        {
            if (record == null) return BadRequest("检验记录不能为空");

            try
            {
                await _service.AddRecordAsync(orderId, record);
                return Ok();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (DbUpdateException)
            {
                return StatusCode(503, "数据库繁忙或写入失败，请稍后重试");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpPut("records/{recordId}/audit")]
        public async Task<IActionResult> AuditRecord(string recordId, [FromBody] RecordAuditDto auditData)
        {
            if (auditData == null)
                return BadRequest("审核数据不能为空");

            if (string.IsNullOrEmpty(auditData.AuditorName))
                return BadRequest("审核人姓名不能为空");

            if (auditData.Status != 1 && auditData.Status != 2)
                return BadRequest("审核状态无效");

            try
            {
                await _service.AuditRecordAsync(recordId, auditData);
                return Ok(new { message = "审核完成" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"服务器错误: {ex.Message}");
            }
        }

        [HttpDelete("records/{recordId}")]
        public async Task<IActionResult> DeleteRecord(string recordId)
        {
            try
            {
                await _service.DeleteRecordAsync(recordId);
                return Ok(new { message = "删除成功", recordId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
