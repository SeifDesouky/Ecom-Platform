using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Customers;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        // Staff وفوق — يشوف customers الـ tenant
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId, [FromQuery] PaginationParams pagination)
        {
            var result = await _customerService.GetAllByTenantAsync(tenantId, pagination);
            return Ok(result);
        }

        // Staff وفوق — يشوف customer معين
        [HttpGet("{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _customerService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // Staff وفوق — إضافة customer جديد
        [HttpPost]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
        {
            var result = await _customerService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // Staff وفوق — تعديل بيانات customer
        [HttpPut("{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerDto dto)
        {
            var result = await _customerService.UpdateAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — حذف customer (عملية حساسة)
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _customerService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تفعيل/تعطيل customer
        [HttpPatch("{id}/toggle-status")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _customerService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // Staff وفوق — إضافة عنوان لـ customer
        [HttpPost("address")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> AddAddress([FromBody] CreateCustomerAddressDto dto)
        {
            var result = await _customerService.AddAddressAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // Staff وفوق — حذف عنوان
        [HttpDelete("address/{addressId}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> DeleteAddress(Guid addressId)
        {
            var result = await _customerService.DeleteAddressAsync(addressId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // Staff وفوق — تحديد العنوان الافتراضي
        [HttpPatch("address/{addressId}/set-default")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> SetDefaultAddress(Guid addressId)
        {
            var result = await _customerService.SetDefaultAddressAsync(addressId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}