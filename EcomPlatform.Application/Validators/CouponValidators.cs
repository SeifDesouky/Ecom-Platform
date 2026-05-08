using EcomPlatform.Application.DTOs.Coupons;
using EcomPlatform.Core.Enums;
using FluentValidation;

namespace EcomPlatform.Application.Validators
{
    public class CreateCouponValidator : AbstractValidator<CreateCouponDto>
    {
        public CreateCouponValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("كود الكوبون مطلوب")
                .MaximumLength(50)
                .Matches(@"^[A-Za-z0-9\-_]+$")
                .WithMessage("كود الكوبون يقبل أحرف وأرقام وشرطة فقط");

            RuleFor(x => x.Value)
                .GreaterThan(0).WithMessage("قيمة الكوبون لازم تكون أكبر من صفر");

            // لو Percentage، القيمة لا تتجاوز 100
            RuleFor(x => x.Value)
                .LessThanOrEqualTo(100)
                .WithMessage("نسبة الخصم لا تتجاوز 100%")
                .When(x => x.Type == CouponType.Percentage);

            RuleFor(x => x.MinOrderAmount)
                .GreaterThan(0)
                .When(x => x.MinOrderAmount.HasValue)
                .WithMessage("الحد الأدنى للطلب لازم يكون أكبر من صفر");

            RuleFor(x => x.MaxDiscountAmount)
                .GreaterThan(0)
                .When(x => x.MaxDiscountAmount.HasValue)
                .WithMessage("الحد الأقصى للخصم لازم يكون أكبر من صفر");

            RuleFor(x => x.UsageLimit)
                .GreaterThan(0)
                .When(x => x.UsageLimit.HasValue)
                .WithMessage("حد الاستخدام لازم يكون أكبر من صفر");

            // لو في StartDate وEndDate، EndDate لازم يكون بعد StartDate
            RuleFor(x => x.EndDate)
                .GreaterThan(x => x.StartDate)
                .WithMessage("تاريخ الانتهاء لازم يكون بعد تاريخ البداية")
                .When(x => x.StartDate.HasValue && x.EndDate.HasValue);

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");
        }
    }

    public class ValidateCouponValidator : AbstractValidator<ValidateCouponDto>
    {
        public ValidateCouponValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("كود الكوبون مطلوب");

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");

            RuleFor(x => x.OrderAmount)
                .GreaterThan(0).WithMessage("مبلغ الطلب لازم يكون أكبر من صفر");
        }
    }
}
