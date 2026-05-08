using EcomPlatform.Application.DTOs.Categories;
using FluentValidation;

namespace EcomPlatform.Application.Validators
{
    public class CreateCategoryValidator : AbstractValidator<CreateCategoryDto>
    {
        public CreateCategoryValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم التصنيف مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("الـ Slug مطلوب")
                .MaximumLength(100)
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("الـ Slug يقبل أحرف إنجليزية صغيرة وأرقام وشرطة فقط");

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");

            RuleFor(x => x.Image)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrEmpty(x.Image));
        }
    }

    public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryDto>
    {
        public UpdateCategoryValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم التصنيف مطلوب")
                .MaximumLength(100);
        }
    }
}
