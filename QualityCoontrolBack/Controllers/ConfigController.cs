using Microsoft.AspNetCore.Mvc;
using QualityControlAPI.Services.Crimping;

namespace QualityControlAPI.Controllers
{
    [ApiController]
    [Route("api/config")]
    public class ConfigController : ControllerBase
    {
        private readonly CrimpingService _service;

        public ConfigController(CrimpingService service)
        {
            _service = service;
        }

        [HttpGet("terminals")]
        public async Task<IActionResult> GetTerminals() => Ok(await _service.GetTerminalsAsync());

        [HttpGet("wires")]
        public async Task<IActionResult> GetWires() => Ok(await _service.GetWiresAsync());

        [HttpGet("tools")]
        public async Task<IActionResult> GetTools() => Ok(await _service.GetToolsAsync());

        [HttpGet("standards")]
        public async Task<IActionResult> GetStandards() => Ok(await _service.GetStandardsAsync());
    }
}