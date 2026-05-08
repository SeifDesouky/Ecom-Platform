using EcomPlatform.Application.DTOs.Auth;
using FluentValidation;

namespace EcomPlatform.Application.Validators
{
    public class LoginValidator : AbstractValidator<LoginDto>
    {
        public LoginValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة")
                .MaximumLength(256);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("كلمة المرور مطلوبة")
                .MinimumLength(6).WithMessage("كلمة المرور لا تقل عن 6 أحرف");
        }
    }

    public class RegisterValidator : AbstractValidator<RegisterDto>
    {
        public RegisterValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("الاسم الأول مطلوب")
                .MaximumLength(100)
                .Matches(@"^[\p{L}\s\-']+$").WithMessage("الاسم الأول يحتوي على أحرف غير مسموح بها");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("الاسم الأخير مطلوب")
                .MaximumLength(100)
                .Matches(@"^[\p{L}\s\-']+$").WithMessage("الاسم الأخير يحتوي على أحرف غير مسموح بها");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة")
                .MaximumLength(256);

            RuleFor(x => x.Phone)
                .MaximumLength(20)
                .Matches(@"^[\+\d\s\-\(\)]*$").WithMessage("رقم الهاتف غير صحيح")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("كلمة المرور مطلوبة")
                .MinimumLength(8).WithMessage("كلمة المرور لا تقل عن 8 أحرف")
                .MaximumLength(128)
                .Matches(@"[A-Z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف كبير")
                .Matches(@"[a-z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف صغير")
                .Matches(@"\d").WithMessage("كلمة المرور يجب أن تحتوي على رقم");
        }
    }
}
