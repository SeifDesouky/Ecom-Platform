using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Domains;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class DomainsController : ControllerBase
    {
        private readonly ITenantDomainService _domainService;

        public DomainsController(ITenantDomainService domainService)
        {
            _domainService = domainService;
        }

        // TenantAdmin وفوق — يشوف domains الـ tenant
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetByTenant(Guid tenantId)
        {
            var result = await _domainService.GetByTenantAsync(tenantId);
            return Ok(result);
        }

        // TenantAdmin وفوق — يشوف domain معين
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _domainService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — إضافة domain جديد
        [HttpPost]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> AddDomain([FromBody] CreateTenantDomainDto dto)
        {
            var result = await _domainService.AddDomainAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — التحقق من الـ domain
        [HttpPatch("{id}/verify")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Verify(Guid id)
        {
            var result = await _domainService.VerifyDomainAsync(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تفعيل SSL
        [HttpPatch("{id}/enable-ssl")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> EnableSSL(Guid id)
        {
            var result = await _domainService.EnableSSLAsync(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تحديد primary domain
        [HttpPatch("{id}/set-primary")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> SetPrimary(Guid id)
        {
            var result = await _domainService.SetPrimaryAsync(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // SuperAdmin فقط — تغيير status الـ domain (قرار المنصة)
        [HttpPatch("{id}/status")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] DomainStatus status)
        {
            var result = await _domainService.UpdateStatusAsync(id, status);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف domain
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _domainService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
        // SuperAdmin فقط — يشوف كل الـ domains
        [HttpGet("admin/all")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetAllDomains([FromQuery] DomainStatus? status = null)
        {
            var result = await _domainService.GetAllDomainsAsync(status);
            return Ok(result);
        }
    }
}