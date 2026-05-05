using EcomPlatform.Application.DTOs.Plans;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PlansController : ControllerBase
    {
        private readonly IPlanService _planService;

        public PlansController(IPlanService planService)
        {
            _planService = planService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var result = await _planService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _planService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePlanDto dto)
        {
            var result = await _planService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreatePlanDto dto)
        {
            var result = await _planService.UpdateAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _planService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _planService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] CreateSubscriptionDto dto)
        {
            var result = await _planService.SubscribeAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("subscription/tenant/{tenantId}")]
        public async Task<IActionResult> GetTenantSubscription(Guid tenantId)
        {
            var result = await _planService.GetTenantSubscriptionAsync(tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPatch("subscription/{subscriptionId}/cancel")]
        public async Task<IActionResult> CancelSubscription(Guid subscriptionId)
        {
            var result = await _planService.CancelSubscriptionAsync(subscriptionId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPatch("subscription/{subscriptionId}/renew")]
        public async Task<IActionResult> RenewSubscription(Guid subscriptionId)
        {
            var result = await _planService.RenewSubscriptionAsync(subscriptionId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}