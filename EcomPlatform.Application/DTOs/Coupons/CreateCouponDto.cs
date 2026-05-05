using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Coupons
{
    public class CreateCouponDto
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CouponType Type { get; set; } = CouponType.Percentage;
        public decimal Value { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public int? UsageLimit { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid TenantId { get; set; }
    }
}