namespace EcomPlatform.Application.DTOs.Coupons
{
    public class ValidateCouponDto
    {
        public string Code { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public decimal OrderAmount { get; set; }
    }

    public class CouponValidationResponseDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public CouponResponseDto? Coupon { get; set; }
    }
}