using EcomPlatform.Application.DTOs.Orders;
using FluentValidation;

namespace EcomPlatform.Application.Validators
{
    public class CreateOrderValidator : AbstractValidator<CreateOrderDto>
    {
        public CreateOrderValidator()
        {
            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");

            RuleFor(x => x.CustomerName)
                .NotEmpty().WithMessage("اسم العميل مطلوب")
                .MaximumLength(200);

            RuleFor(x => x.CustomerEmail)
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة")
                .MaximumLength(256)
                .When(x => !string.IsNullOrEmpty(x.CustomerEmail));

            RuleFor(x => x.CustomerPhone)
                .MaximumLength(20)
                .Matches(@"^[\+\d\s\-\(\)]*$").WithMessage("رقم هاتف العميل غير صحيح")
                .When(x => !string.IsNullOrEmpty(x.CustomerPhone));

            RuleFor(x => x.ShippingAddress)
                .NotEmpty().WithMessage("عنوان الشحن مطلوب")
                .MaximumLength(500);

            RuleFor(x => x.ShippingCity)
                .NotEmpty().WithMessage("المدينة مطلوبة")
                .MaximumLength(100);

            RuleFor(x => x.ShippingCountry)
                .NotEmpty().WithMessage("البلد مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.ShippingCost)
                .GreaterThanOrEqualTo(0).WithMessage("تكلفة الشحن لا تكون سالبة");

            RuleFor(x => x.Discount)
                .GreaterThanOrEqualTo(0).WithMessage("الخصم لا يكون سالباً");

            RuleFor(x => x.Tax)
                .GreaterThanOrEqualTo(0).WithMessage("الضريبة لا تكون سالبة");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("الطلب لازم يحتوي على منتج واحد على الأقل");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId)
                    .NotEmpty().WithMessage("معرف المنتج مطلوب");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("الكمية لازم تكون أكبر من صفر");

                item.RuleFor(i => i.UnitPrice)
                    .GreaterThan(0).WithMessage("سعر الوحدة لازم يكون أكبر من صفر");
            });
        }
    }
}
