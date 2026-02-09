using Microsoft.AspNetCore.Mvc;
using QualityControlAPI.Services.Crimping;
using QualityControlAPI.Models;

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

        // GET: api/orders
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductionOrder>>> GetAll()
        {
            return Ok(await _service.GetOrdersAsync());
        }

        // GET: api/orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductionOrder>> GetById(string id)
        {
            var order = await _service.GetOrderByIdAsync(id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        // POST: api/orders
        [HttpPost]
        public async Task<ActionResult<ProductionOrder>> Create(ProductionOrder order)
        {
            try
            {
                var result = await _service.CreateOrderAsync(order);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // PUT: api/orders/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, ProductionOrder order)
        {
            if (id != order.Id) return BadRequest("请求ID不一致");
            try
            {
                await _service.UpdateOrderAsync(order);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        // DELETE: api/orders/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _service.DeleteOrderAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        // POST: api/orders/{orderId}/records
        [HttpPost("{orderId}/records")]
        public async Task<IActionResult> AddRecord(string orderId, InspectionRecord record)
        {
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

        // 放在 Controller 命名空间内或单独定义



        // 在 OrdersController 类中添加接口：

        // PUT: api/orders/records/{recordId}/audit
        [HttpPut("records/{recordId}/audit")]
        public async Task<IActionResult> AuditRecord(string recordId, [FromBody] RecordAuditDto auditData)
        {
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

        // PATCH: api/orders/{id}/close
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
    }
}