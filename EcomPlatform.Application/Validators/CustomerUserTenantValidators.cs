using EcomPlatform.Application.DTOs.Customers;
using EcomPlatform.Application.DTOs.Tenants;
using EcomPlatform.Application.DTOs.Users;
using EcomPlatform.Core.Enums;
using FluentValidation;

namespace EcomPlatform.Application.Validators
{
    // ── Customers ────────────────────────────────────────────────────────────

    public class CreateCustomerValidator : AbstractValidator<CreateCustomerDto>
    {
        public CreateCustomerValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("الاسم الأول مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("الاسم الأخير مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة")
                .MaximumLength(256);

            RuleFor(x => x.Phone)
                .MaximumLength(20)
                .Matches(@"^[\+\d\s\-\(\)]*$").WithMessage("رقم الهاتف غير صحيح")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.BirthDate)
                .LessThan(DateTime.UtcNow).WithMessage("تاريخ الميلاد غير صحيح")
                .GreaterThan(new DateTime(1900, 1, 1)).WithMessage("تاريخ الميلاد غير صحيح")
                .When(x => x.BirthDate.HasValue);

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");
        }
    }

    public class UpdateCustomerValidator : AbstractValidator<UpdateCustomerDto>
    {
        public UpdateCustomerValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("الاسم الأول مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("الاسم الأخير مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.Phone)
                .MaximumLength(20)
                .Matches(@"^[\+\d\s\-\(\)]*$")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.BirthDate)
                .LessThan(DateTime.UtcNow)
                .GreaterThan(new DateTime(1900, 1, 1))
                .When(x => x.BirthDate.HasValue);
        }
    }

    public class CreateCustomerAddressValidator : AbstractValidator<CreateCustomerAddressDto>
    {
        public CreateCustomerAddressValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("الاسم الكامل مطلوب")
                .MaximumLength(200);

            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("رقم الهاتف مطلوب")
                .MaximumLength(20)
                .Matches(@"^[\+\d\s\-\(\)]*$");

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("العنوان مطلوب")
                .MaximumLength(500);

            RuleFor(x => x.City)
                .NotEmpty().WithMessage("المدينة مطلوبة")
                .MaximumLength(100);

            RuleFor(x => x.Country)
                .NotEmpty().WithMessage("البلد مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.PostalCode)
                .MaximumLength(20)
                .When(x => !string.IsNullOrEmpty(x.PostalCode));

            RuleFor(x => x.CustomerId)
                .NotEmpty().WithMessage("معرف العميل مطلوب");
        }
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    public class CreateUserValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("الاسم الأول مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("الاسم الأخير مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة")
                .MaximumLength(256);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("كلمة المرور مطلوبة")
                .MinimumLength(8).WithMessage("كلمة المرور لا تقل عن 8 أحرف")
                .Matches(@"[A-Z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف كبير")
                .Matches(@"[a-z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف صغير")
                .Matches(@"\d").WithMessage("كلمة المرور يجب أن تحتوي على رقم");

            // SuperAdmin لا يتعين إلا يدوياً من الـ DB
            RuleFor(x => x.Role)
                .NotEqual(UserRole.SuperAdmin)
                .WithMessage("لا يمكن إنشاء SuperAdmin عن طريق الـ API");
        }
    }

    public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("الاسم الأول مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("الاسم الأخير مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.Role)
                .NotEqual(UserRole.SuperAdmin)
                .WithMessage("لا يمكن تعيين دور SuperAdmin عن طريق الـ API");
        }
    }

    // ── Tenants ───────────────────────────────────────────────────────────────

    public class CreateTenantValidator : AbstractValidator<CreateTenantDto>
    {
        public CreateTenantValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم المتجر مطلوب")
                .MaximumLength(200);

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("الـ Slug مطلوب")
                .MaximumLength(100)
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("الـ Slug يقبل أحرف إنجليزية صغيرة وأرقام وشرطة فقط");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة")
                .MaximumLength(256);

            RuleFor(x => x.Phone)
                .MaximumLength(20)
                .Matches(@"^[\+\d\s\-\(\)]*$")
                .When(x => !string.IsNullOrEmpty(x.Phone));
        }
    }

    public class UpdateTenantValidator : AbstractValidator<UpdateTenantDto>
    {
        public UpdateTenantValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم المتجر مطلوب")
                .MaximumLength(200);

            RuleFor(x => x.Phone)
                .MaximumLength(20)
                .Matches(@"^[\+\d\s\-\(\)]*$")
                .When(x => !string.IsNullOrEmpty(x.Phone));
        }
    }
}
