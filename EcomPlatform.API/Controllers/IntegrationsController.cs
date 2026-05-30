using Asp.Versioning;
using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [Authorize]
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/integrations")]
    public class IntegrationsController : ControllerBase
    {
        private readonly IIntegrationService _integrationService;

        public IntegrationsController(IIntegrationService integrationService)
        {
            _integrationService = integrationService;
        }

        // ── CRUD ─────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateIntegrationDto dto)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty)
                return Unauthorized();

            var result = await _integrationService.CreateAsync(dto, tenantId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty)
                return Unauthorized();

            var result = await _integrationService.GetAllAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _integrationService.GetByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIntegrationDto dto)
        {
            var result = await _integrationService.UpdateAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _integrationService.DeleteAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Connection ───────────────────────────────────────────────────────

        [HttpPost("{id:guid}/test-connection")]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            var result = await _integrationService.TestConnectionAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            var result = await _integrationService.ActivateAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var result = await _integrationService.DeactivateAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Sync ─────────────────────────────────────────────────────────────

        [HttpPost("{id:guid}/sync")]
        public async Task<IActionResult> Sync(Guid id, [FromBody] SyncRequestDto dto)
        {
            var result = await _integrationService.SyncAsync(
                id,
                dto.EntityType,
                dto.Direction,
                isManual: true);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id:guid}/sync-logs")]
        public async Task<IActionResult> GetSyncLogs(
            Guid id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _integrationService.GetSyncLogsAsync(id, page, pageSize);
            return Ok(result);
        }

        // ── Platforms ────────────────────────────────────────────────────────

        [HttpGet("supported-platforms")]
        public async Task<IActionResult> GetSupportedPlatforms(
            [FromServices] IAdapterFactory adapterFactory)
        {
            var platforms = adapterFactory.GetSupportedPlatforms();
            return Ok(platforms);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Guid GetTenantId()
        {
            var claim = User.Claims.FirstOrDefault(c =>
                c.Type == "tenantId" ||
                c.Type == "TenantId");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
        }
    }
}