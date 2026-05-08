using EcomPlatform.Application.DTOs.Products;
using FluentValidation;

namespace EcomPlatform.Application.Validators
{
    public class CreateProductValidator : AbstractValidator<CreateProductDto>
    {
        public CreateProductValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم المنتج مطلوب")
                .MaximumLength(200).WithMessage("اسم المنتج لا يتجاوز 200 حرف");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("الـ Slug مطلوب")
                .MaximumLength(200)
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("الـ Slug يقبل أحرف إنجليزية صغيرة وأرقام وشرطة فقط");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("السعر لازم يكون أكبر من صفر");

            RuleFor(x => x.ComparePrice)
                .GreaterThan(x => x.Price)
                .WithMessage("سعر المقارنة لازم يكون أكبر من سعر البيع")
                .When(x => x.ComparePrice.HasValue);

            RuleFor(x => x.CostPrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر التكلفة لا يكون سالباً")
                .When(x => x.CostPrice.HasValue);

            RuleFor(x => x.Stock)
                .GreaterThanOrEqualTo(0).WithMessage("الكمية لا يمكن أن تكون سالبة");

            RuleFor(x => x.LowStockAlert)
                .GreaterThanOrEqualTo(0).WithMessage("حد التنبيه لا يكون سالباً");

            RuleFor(x => x.Weight)
                .GreaterThanOrEqualTo(0).WithMessage("الوزن لا يكون سالباً");

            RuleFor(x => x.SKU)
                .MaximumLength(100);

            RuleFor(x => x.Barcode)
                .MaximumLength(100);

            RuleFor(x => x.MetaTitle)
                .MaximumLength(160);

            RuleFor(x => x.MetaDescription)
                .MaximumLength(320);

            RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage("التصنيف مطلوب");

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");

            RuleForEach(x => x.Images).ChildRules(image =>
            {
                image.RuleFor(i => i.Url)
                    .NotEmpty().WithMessage("رابط الصورة مطلوب")
                    .MaximumLength(1000);

                image.RuleFor(i => i.SortOrder)
                    .GreaterThanOrEqualTo(0);
            });
        }
    }

    public class UpdateProductValidator : AbstractValidator<UpdateProductDto>
    {
        public UpdateProductValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم المنتج مطلوب")
                .MaximumLength(200);

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("السعر لازم يكون أكبر من صفر");

            RuleFor(x => x.ComparePrice)
                .GreaterThan(x => x.Price)
                .WithMessage("سعر المقارنة لازم يكون أكبر من سعر البيع")
                .When(x => x.ComparePrice.HasValue);

            RuleFor(x => x.CostPrice)
                .GreaterThanOrEqualTo(0)
                .When(x => x.CostPrice.HasValue);

            RuleFor(x => x.Stock)
                .GreaterThanOrEqualTo(0).WithMessage("الكمية لا يمكن أن تكون سالبة");

            RuleFor(x => x.LowStockAlert)
                .GreaterThanOrEqualTo(0);

            RuleFor(x => x.Weight)
                .GreaterThanOrEqualTo(0);

            RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage("التصنيف مطلوب");
        }
    }
}
