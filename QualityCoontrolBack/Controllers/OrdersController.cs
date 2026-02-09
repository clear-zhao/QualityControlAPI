using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // 确保引用这个
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductionOrder>>> GetOrders()
        {
            return Ok(await _service.GetOrdersAsync());
        }

        // 必须补充这个方法，否则 CreateOrder 里的 CreatedAtAction 会报错
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductionOrder>> GetOrder(string id)
        {
            var orders = await _service.GetOrdersAsync();
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        [HttpPost]
        public async Task<ActionResult<ProductionOrder>> CreateOrder(ProductionOrder order)
        {
            try
            {
                // 直接把前端传来的 order 存进去，因为现在字段完全匹配了
                var createdOrder = await _service.CreateOrderAsync(order);
                return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
            }
            catch (Exception ex)
            {
                // 打印详细错误到控制台，方便你调试
                Console.WriteLine($"Error creating order: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }

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
                Console.WriteLine($"Error adding record: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }
    }
}