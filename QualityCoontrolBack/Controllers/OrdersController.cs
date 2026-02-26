using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductionOrder>>> GetAll()
            => Ok(await _service.GetOrdersAsync());

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductionOrder>> GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("订单ID不能为空");

            var order = await _service.GetOrderByIdAsync(id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        [HttpGet("orders/by-creator-employee")]
        public async Task<IActionResult> GetOrdersByCreatorEmployeeId(
            [FromQuery] string employeeId,
            [FromQuery] bool includeClosed = true)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return BadRequest("employeeId 不能为空");
            }

            var list = await _service.GetOrdersByCreatorEmployeeIdAsync(employeeId, includeClosed);
            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<ProductionOrder>> Create([FromBody] ProductionOrder order)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Id) || string.IsNullOrWhiteSpace(order.ProductionOrderNo))
            {
                return BadRequest("订单ID和生产单号不能为空");
            }

            try
            {
                var result = await _service.CreateOrderAsync(order);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ProductionOrder order)
        {
            if (order == null) return BadRequest("请求体不能为空");
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
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("订单ID不能为空");

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

        [HttpPatch("{id}/close")]
        public async Task<IActionResult> CloseOrder(string id, [FromBody] bool isClosed)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("订单ID不能为空");

            try
            {
                await _service.ToggleOrderCloseStatusAsync(id, isClosed);
                return Ok(new { message = isClosed ? "订单已关闭" : "订单已重新激活" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

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
        }

        [HttpPost("{orderId}/records")]
        public async Task<IActionResult> AddRecord(string orderId, [FromBody] InspectionRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Id))
            {
                return BadRequest("记录ID不能为空");
            }

            try
            {
                await _service.AddRecordAsync(orderId, record);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("records/{recordId}/audit")]
        public async Task<IActionResult> AuditRecord(string recordId, [FromBody] RecordAuditDto auditData)
        {
            if (string.IsNullOrWhiteSpace(recordId)) return BadRequest("recordId 不能为空");
            if (auditData == null) return BadRequest("请求体不能为空");
            if (string.IsNullOrWhiteSpace(auditData.AuditorName)) return BadRequest("审核人姓名不能为空");
            if (auditData.Status != 1 && auditData.Status != 2) return BadRequest("审核状态无效");

            try
            {
                await _service.AuditRecordAsync(recordId, auditData);
                return Ok(new { message = "审核完成" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
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
