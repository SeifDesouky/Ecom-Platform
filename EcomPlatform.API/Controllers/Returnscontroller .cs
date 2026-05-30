using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Returns;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [Route("api/v1/returns")]
    [Authorize]
    public class ReturnsController : ControllerBase
    {
        private readonly IReturnService _returnService;

        public ReturnsController(IReturnService returnService)
        {
            _returnService = returnService;
        }

        /// <summary>كل طلبات الإرجاع للـ tenant</summary>
        [HttpGet("tenant/{tenantId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetAll(Guid tenantId, [FromQuery] PaginationParams pagination)
            => Ok(await _returnService.GetByTenantAsync(tenantId, pagination));

        /// <summary>طلبات إرجاع أوردر معين</summary>
        [HttpGet("order/{orderId:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetByOrder(Guid orderId)
            => Ok(await _returnService.GetByOrderAsync(orderId));

        /// <summary>طلب إرجاع بالـ ID</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _returnService.GetByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>طلب إرجاع بالرقم</summary>
        [HttpGet("number/{returnNumber}")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> GetByNumber(string returnNumber)
        {
            var result = await _returnService.GetByReturnNumberAsync(returnNumber);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>إنشاء طلب إرجاع — من العميل أو الـ Admin</summary>
        [HttpPost]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> Create([FromBody] CreateReturnRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _returnService.CreateAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>مراجعة الطلب — Admin فقط (قبول أو رفض)</summary>
        [HttpPatch("{id:guid}/review")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> Review(Guid id, [FromBody] ReviewReturnRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _returnService.ReviewAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>تنفيذ الاسترداد المالي — Admin فقط</summary>
        [HttpPost("refund")]
        [Authorize(Policy = Policies.TenantAdminOrAbove)]
        public async Task<IActionResult> ProcessRefund([FromBody] ProcessRefundDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _returnService.ProcessRefundAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>إلغاء طلب الإرجاع من العميل</summary>
        [HttpPatch("{id:guid}/cancel")]
        [Authorize(Policy = Policies.TenantStaffOrAbove)]
        public async Task<IActionResult> CancelByCustomer(Guid id)
        {
            var result = await _returnService.CancelByCustomerAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}