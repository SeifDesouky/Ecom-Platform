using EcomPlatform.Application.DTOs.Customers;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetAllByTenant(Guid tenantId)
        {
            var result = await _customerService.GetAllByTenantAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _customerService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
        {
            var result = await _customerService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerDto dto)
        {
            var result = await _customerService.UpdateAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _customerService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _customerService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("address")]
        public async Task<IActionResult> AddAddress([FromBody] CreateCustomerAddressDto dto)
        {
            var result = await _customerService.AddAddressAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("address/{addressId}")]
        public async Task<IActionResult> DeleteAddress(Guid addressId)
        {
            var result = await _customerService.DeleteAddressAsync(addressId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("address/{addressId}/set-default")]
        public async Task<IActionResult> SetDefaultAddress(Guid addressId)
        {
            var result = await _customerService.SetDefaultAddressAsync(addressId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}