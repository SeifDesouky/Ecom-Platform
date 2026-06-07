using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Plans;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class PlansController : ControllerBase
    {
        private readonly IPlanService _planService;

        public PlansController(IPlanService planService)
        {
            _planService = planService;
        }

        // AllowAnonymous — الـ pricing page عامة للكل
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var result = await _planService.GetAllAsync();
            return Ok(result);
        }

        // AllowAnonymous — تفاصيل plan معينة
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _planService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // SuperAdmin فقط — إنشاء plan جديد
        [HttpPost]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Create([FromBody] CreatePlanDto dto)
        {
            var result = await _planService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // SuperAdmin فقط — تعديل plan
        [HttpPut("{id}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreatePlanDto dto)
        {
            var result = await _planService.UpdateAsync(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // SuperAdmin فقط — حذف plan
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _planService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // SuperAdmin فقط — تفعيل/تعطيل plan
        [HttpPatch("{id}/toggle-status")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var result = await _planService.ToggleStatusAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // ─── Subscriptions ────────────────────────────────────────────────────

        // TenantAdmin وفوق — الاشتراك في plan
        [HttpPost("subscribe")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Subscribe([FromBody] CreateSubscriptionDto dto)
        {
            var result = await _planService.SubscribeAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — يشوف subscription الـ tenant
        [HttpGet("subscription/tenant/{tenantId}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> GetTenantSubscription(Guid tenantId)
        {
            var result = await _planService.GetTenantSubscriptionAsync(tenantId);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — إلغاء subscription
        [HttpPatch("subscription/{subscriptionId}/cancel")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CancelSubscription(Guid subscriptionId)
        {
            var result = await _planService.CancelSubscriptionAsync(subscriptionId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // TenantAdmin وفوق — تجديد subscription
        [HttpPatch("subscription/{subscriptionId}/renew")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> RenewSubscription(Guid subscriptionId)
        {
            var result = await _planService.RenewSubscriptionAsync(subscriptionId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
        // SuperAdmin فقط — كل الـ subscriptions في المنصة
        [HttpGet("subscription/all")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> GetAllSubscriptions(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20)
        {
            var result = await _planService.GetAllSubscriptionsAsync(page, limit);
            return Ok(result);
        }
    }
}