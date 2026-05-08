using EcomPlatform.Application.DTOs.CMS;
using EcomPlatform.Application.DTOs.Domains;
using EcomPlatform.Application.DTOs.Notifications;
using EcomPlatform.Application.DTOs.Plans;
using EcomPlatform.Application.DTOs.Settings;
using EcomPlatform.Application.DTOs.Shipping;
using EcomPlatform.Application.DTOs.Tickets;
using FluentValidation;

namespace EcomPlatform.Application.Validators
{
    // ── Plans & Subscriptions ─────────────────────────────────────────────────

    public class CreatePlanValidator : AbstractValidator<CreatePlanDto>
    {
        public CreatePlanValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الباقة مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.MonthlyPrice)
                .GreaterThanOrEqualTo(0).WithMessage("السعر الشهري لا يكون سالباً");

            RuleFor(x => x.YearlyPrice)
                .GreaterThanOrEqualTo(0).WithMessage("السعر السنوي لا يكون سالباً");

            RuleFor(x => x.MaxProducts)
                .GreaterThanOrEqualTo(-1).WithMessage("الحد الأقصى للمنتجات: -1 = غير محدود، أو عدد موجب");

            RuleFor(x => x.MaxOrders)
                .GreaterThanOrEqualTo(-1);

            RuleFor(x => x.MaxCustomers)
                .GreaterThanOrEqualTo(-1);

            RuleFor(x => x.MaxUsers)
                .GreaterThan(0).WithMessage("الحد الأقصى للمستخدمين لازم يكون على الأقل 1");
        }
    }

    public class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionDto>
    {
        public CreateSubscriptionValidator()
        {
            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");

            RuleFor(x => x.PlanId)
                .NotEmpty().WithMessage("معرف الباقة مطلوب");
        }
    }

    // ── Tickets ───────────────────────────────────────────────────────────────

    public class CreateTicketValidator : AbstractValidator<CreateTicketDto>
    {
        public CreateTicketValidator()
        {
            RuleFor(x => x.Subject)
                .NotEmpty().WithMessage("موضوع التذكرة مطلوب")
                .MaximumLength(200);

            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("رسالة التذكرة مطلوبة")
                .MaximumLength(5000);

            RuleFor(x => x.Category)
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.Category));

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");

            RuleFor(x => x.CreatedById)
                .NotEmpty().WithMessage("معرف المنشئ مطلوب");
        }
    }

    public class CreateTicketReplyValidator : AbstractValidator<CreateTicketReplyDto>
    {
        public CreateTicketReplyValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("الرد مطلوب")
                .MaximumLength(5000);

            RuleFor(x => x.TicketId)
                .NotEmpty().WithMessage("معرف التذكرة مطلوب");

            RuleFor(x => x.CreatedById)
                .NotEmpty().WithMessage("معرف المرسل مطلوب");
        }
    }

    // ── Shipping ──────────────────────────────────────────────────────────────

    public class CreateShippingZoneValidator : AbstractValidator<CreateShippingZoneDto>
    {
        public CreateShippingZoneValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم نطاق الشحن مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");
        }
    }

    public class CreateShippingMethodValidator : AbstractValidator<CreateShippingMethodDto>
    {
        public CreateShippingMethodValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم طريقة الشحن مطلوب")
                .MaximumLength(100);

            RuleFor(x => x.Cost)
                .GreaterThanOrEqualTo(0).WithMessage("تكلفة الشحن لا تكون سالبة");

            RuleFor(x => x.MinOrderAmount)
                .GreaterThanOrEqualTo(0)
                .When(x => x.MinOrderAmount.HasValue);

            RuleFor(x => x.MaxOrderAmount)
                .GreaterThan(x => x.MinOrderAmount ?? 0)
                .WithMessage("الحد الأقصى لازم يكون أكبر من الحد الأدنى")
                .When(x => x.MaxOrderAmount.HasValue && x.MinOrderAmount.HasValue);

            RuleFor(x => x.EstimatedDaysMin)
                .GreaterThan(0).When(x => x.EstimatedDaysMin.HasValue);

            RuleFor(x => x.EstimatedDaysMax)
                .GreaterThanOrEqualTo(x => x.EstimatedDaysMin ?? 1)
                .WithMessage("الحد الأقصى للأيام لازم يكون أكبر من أو يساوي الحد الأدنى")
                .When(x => x.EstimatedDaysMax.HasValue && x.EstimatedDaysMin.HasValue);

            RuleFor(x => x.ShippingZoneId)
                .NotEmpty().WithMessage("معرف نطاق الشحن مطلوب");
        }
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    public class CreateNotificationValidator : AbstractValidator<CreateNotificationDto>
    {
        public CreateNotificationValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("عنوان الإشعار مطلوب")
                .MaximumLength(200);

            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("نص الإشعار مطلوب")
                .MaximumLength(1000);

            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("معرف المستخدم مطلوب");

            RuleFor(x => x.ActionUrl)
                .MaximumLength(2000)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("رابط الإجراء غير صحيح")
                .When(x => !string.IsNullOrEmpty(x.ActionUrl));
        }
    }

    // ── CMS ───────────────────────────────────────────────────────────────────

    public class CreateArticleValidator : AbstractValidator<CreateArticleDto>
    {
        public CreateArticleValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("عنوان المقال مطلوب")
                .MaximumLength(300);

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("الـ Slug مطلوب")
                .MaximumLength(300)
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("الـ Slug يقبل أحرف إنجليزية صغيرة وأرقام وشرطة فقط");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("محتوى المقال مطلوب");

            RuleFor(x => x.MetaTitle)
                .MaximumLength(160);

            RuleFor(x => x.MetaDescription)
                .MaximumLength(320);

            RuleFor(x => x.AuthorId)
                .NotEmpty().WithMessage("معرف الكاتب مطلوب");
        }
    }

    public class CreatePageValidator : AbstractValidator<CreatePageDto>
    {
        public CreatePageValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("عنوان الصفحة مطلوب")
                .MaximumLength(300);

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("الـ Slug مطلوب")
                .MaximumLength(300)
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("الـ Slug يقبل أحرف إنجليزية صغيرة وأرقام وشرطة فقط");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("محتوى الصفحة مطلوب");

            RuleFor(x => x.SortOrder)
                .GreaterThanOrEqualTo(0);
        }
    }

    // ── Domains ───────────────────────────────────────────────────────────────

    public class CreateTenantDomainValidator : AbstractValidator<CreateTenantDomainDto>
    {
        public CreateTenantDomainValidator()
        {
            RuleFor(x => x.Domain)
                .NotEmpty().WithMessage("الدومين مطلوب")
                .MaximumLength(253)
                .Matches(@"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$")
                .WithMessage("صيغة الدومين غير صحيحة");

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("معرف المتجر مطلوب");
        }
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public class CreateSettingValidator : AbstractValidator<CreateSettingDto>
    {
        public CreateSettingValidator()
        {
            RuleFor(x => x.Key)
                .NotEmpty().WithMessage("مفتاح الإعداد مطلوب")
                .MaximumLength(200)
                .Matches(@"^[a-zA-Z0-9_\.]+$")
                .WithMessage("المفتاح يقبل أحرف وأرقام وشرطة سفلية ونقطة فقط");

            RuleFor(x => x.Value)
                .NotNull().WithMessage("قيمة الإعداد مطلوبة");

            RuleFor(x => x.Group)
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.Group));
        }
    }

    public class BulkUpdateSettingValidator : AbstractValidator<BulkUpdateSettingDto>
    {
        public BulkUpdateSettingValidator()
        {
            RuleFor(x => x.Settings)
                .NotEmpty().WithMessage("لازم ترسل إعداد واحد على الأقل")
                .Must(d => d.Count <= 100).WithMessage("الحد الأقصى 100 إعداد في الطلب الواحد");
        }
    }
}
