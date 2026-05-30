using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.EmailMarketing;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/email-marketing")]
    [Authorize]
    public class EmailMarketingController : ControllerBase
    {
        private readonly IEmailMarketingService _service;
        private readonly IAuditLogService _auditLogService;

        public EmailMarketingController(
            IEmailMarketingService service,
            IAuditLogService auditLogService)
        {
            _service = service;
            _auditLogService = auditLogService;
        }

        private Guid? GetUserId()
            => Guid.TryParse(User.FindFirstValue("userId"), out var id) ? id : null;
        private Guid? GetTenantId()
            => Guid.TryParse(User.FindFirstValue("tenantId"), out var id) ? id : null;
        private string GetIp()
            => HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        // ════════════════════════════════════════════════════════════════
        // MAILING LISTS
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/email-marketing/lists?tenantId=xxx
        [HttpGet("lists")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetLists(
            [FromQuery] Guid tenantId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _service.GetListsAsync(tenantId, pagination);
            return Ok(result);
        }

        // GET /api/v1/email-marketing/lists/{id}
        [HttpGet("lists/{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetListById(Guid id)
        {
            var result = await _service.GetListByIdAsync(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // POST /api/v1/email-marketing/lists
        [HttpPost("lists")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateList([FromBody] CreateMailingListDto dto)
        {
            var result = await _service.CreateListAsync(dto);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync("MailingList", result.Data!.Id.ToString(),
                AuditAction.Create, GetUserId() ?? Guid.Empty, dto.TenantId,
                newValue: $"List '{result.Data.Name}' created", ipAddress: GetIp());

            return Ok(result);
        }

        // PUT /api/v1/email-marketing/lists/{id}
        [HttpPut("lists/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateList(Guid id, [FromBody] UpdateMailingListDto dto)
        {
            var result = await _service.UpdateListAsync(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // DELETE /api/v1/email-marketing/lists/{id}
        [HttpDelete("lists/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteList(Guid id)
        {
            var result = await _service.DeleteListAsync(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // SUBSCRIBERS
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/email-marketing/lists/{listId}/subscribers
        [HttpGet("lists/{listId}/subscribers")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetSubscribers(
            Guid listId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _service.GetSubscribersAsync(listId, pagination);
            return Ok(result);
        }

        // POST /api/v1/email-marketing/subscribers
        [HttpPost("subscribers")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> AddSubscriber([FromBody] AddSubscriberDto dto)
        {
            var result = await _service.AddSubscriberAsync(dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // POST /api/v1/email-marketing/subscribers/import
        [HttpPost("subscribers/import")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> ImportSubscribers([FromBody] ImportSubscribersDto dto)
        {
            var result = await _service.ImportSubscribersAsync(dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // DELETE /api/v1/email-marketing/subscribers/{id}
        [HttpDelete("subscribers/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteSubscriber(Guid id)
        {
            var result = await _service.DeleteSubscriberAsync(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // PATCH /api/v1/email-marketing/subscribers/{id}/unsubscribe
        [HttpPatch("subscribers/{id}/unsubscribe")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Unsubscribe(Guid id)
        {
            var result = await _service.UnsubscribeAsync(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // GET /api/v1/email-marketing/unsubscribe/{token}
        // Public — رابط إلغاء الاشتراك من الإيميل
        [HttpGet("unsubscribe/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> UnsubscribeByToken(string token)
        {
            var result = await _service.UnsubscribeByTokenAsync(token);
            if (!result.Success)
                return Content("<h2>Invalid or expired unsubscribe link.</h2>", "text/html");

            // صفحة تأكيد بسيطة
            return Content("""
                <html><body style="font-family:sans-serif;text-align:center;padding:60px">
                    <h2>✅ You have been unsubscribed successfully.</h2>
                    <p>You will no longer receive emails from this list.</p>
                </body></html>
                """, "text/html");
        }

        // ════════════════════════════════════════════════════════════════
        // CAMPAIGNS
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/email-marketing/campaigns?tenantId=xxx
        [HttpGet("campaigns")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetCampaigns(
            [FromQuery] Guid tenantId,
            [FromQuery] PaginationParams pagination)
        {
            var result = await _service.GetCampaignsAsync(tenantId, pagination);
            return Ok(result);
        }

        // GET /api/v1/email-marketing/campaigns/{id}
        [HttpGet("campaigns/{id}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetCampaignById(Guid id)
        {
            var result = await _service.GetCampaignByIdAsync(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // POST /api/v1/email-marketing/campaigns
        [HttpPost("campaigns")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignDto dto)
        {
            var result = await _service.CreateCampaignAsync(dto);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync("Campaign", result.Data!.Id.ToString(),
                AuditAction.Create, GetUserId() ?? Guid.Empty, dto.TenantId,
                newValue: $"Campaign '{result.Data.Name}' created", ipAddress: GetIp());

            return Ok(result);
        }

        // PUT /api/v1/email-marketing/campaigns/{id}
        [HttpPut("campaigns/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateCampaignDto dto)
        {
            var result = await _service.UpdateCampaignAsync(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // DELETE /api/v1/email-marketing/campaigns/{id}
        [HttpDelete("campaigns/{id}")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> DeleteCampaign(Guid id)
        {
            var result = await _service.DeleteCampaignAsync(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // POST /api/v1/email-marketing/campaigns/{id}/send
        [HttpPost("campaigns/{id}/send")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> SendCampaign(Guid id)
        {
            var result = await _service.SendCampaignAsync(id);
            if (!result.Success) return BadRequest(result);

            await _auditLogService.LogAsync("Campaign", id.ToString(),
                AuditAction.StatusChange, GetUserId() ?? Guid.Empty, GetTenantId(),
                newValue: "Campaign sent", ipAddress: GetIp());

            return Ok(result);
        }

        // PATCH /api/v1/email-marketing/campaigns/{id}/cancel
        [HttpPatch("campaigns/{id}/cancel")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> CancelCampaign(Guid id)
        {
            var result = await _service.CancelCampaignAsync(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════════
        // TRACKING — Public (بدون Auth — يُستدعى من الإيميل مباشرةً)
        // ════════════════════════════════════════════════════════════════

        // GET /api/v1/email-marketing/track/open/{token}
        // Tracking Pixel — يُضمَّن في الإيميل كـ <img>
        [HttpGet("track/open/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackOpen(string token)
        {
            await _service.TrackOpenAsync(token);
            // إرجاع 1×1 Transparent GIF
            var gif = Convert.FromBase64String(
                "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
            return File(gif, "image/gif");
        }

        // GET /api/v1/email-marketing/track/click/{token}?url=https://...
        // Redirect مع تسجيل النقرة
        [HttpGet("track/click/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackClick(string token, [FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("URL is required.");

            await _service.TrackClickAsync(token, url);
            return Redirect(url);
        }
    }
}