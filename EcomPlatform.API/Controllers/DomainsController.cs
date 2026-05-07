using EcomPlatform.Application.DTOs.Domains;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DomainsController : ControllerBase
    {
        private readonly ITenantDomainService _domainService;

        public DomainsController(ITenantDomainService domainService)
        {
            _domainService = domainService;
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetByTenant(Guid tenantId)
        {
            var result = await _domainService.GetByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _domainService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> AddDomain([FromBody] CreateTenantDomainDto dto)
        {
            var result = await _domainService.AddDomainAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/verify")]
        public async Task<IActionResult> Verify(Guid id)
        {
            var result = await _domainService.VerifyDomainAsync(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/enable-ssl")]
        public async Task<IActionResult> EnableSSL(Guid id)
        {
            var result = await _domainService.EnableSSLAsync(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/set-primary")]
        public async Task<IActionResult> SetPrimary(Guid id)
        {
            var result = await _domainService.SetPrimaryAsync(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] DomainStatus status)
        {
            var result = await _domainService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _domainService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}