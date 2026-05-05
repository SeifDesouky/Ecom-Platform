using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Coupons
{
    public class CouponResponseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CouponType Type { get; set; }
        public decimal Value { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public int? UsageLimit { get; set; }
        public int UsageCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}