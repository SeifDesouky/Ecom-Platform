using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Notifications;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // AnyAuthenticatedUser — كل user يشوف notifications بتاعته
        [HttpGet("user/{userId}")]
        [Authorize(Policy = Policies.AnyAuthenticatedUser)]
        public async Task<IActionResult> GetByUser(Guid userId)
        {
            var result = await _notificationService.GetByUserAsync(userId);
            return Ok(result);
        }

        // AnyAuthenticatedUser — عدد الـ unread
        [HttpGet("user/{userId}/unread-count")]
        [Authorize(Policy = Policies.AnyAuthenticatedUser)]
        public async Task<IActionResult> GetUnreadCount(Guid userId)
        {
            var result = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(result);
        }

        // TenantAdmin وفوق — إنشاء notification (إرسال لـ users)
        [HttpPost]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreateNotificationDto dto)
        {
            var result = await _notificationService.CreateAsync(dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        // AnyAuthenticatedUser — mark notification كمقروء
        [HttpPatch("{id}/mark-read")]
        [Authorize(Policy = Policies.AnyAuthenticatedUser)]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var result = await _notificationService.MarkAsReadAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        // AnyAuthenticatedUser — mark all كمقروء
        [HttpPatch("user/{userId}/mark-all-read")]
        [Authorize(Policy = Policies.AnyAuthenticatedUser)]
        public async Task<IActionResult> MarkAllAsRead(Guid userId)
        {
            var result = await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(result);
        }

        // AnyAuthenticatedUser — حذف notification
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.AnyAuthenticatedUser)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _notificationService.DeleteAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}