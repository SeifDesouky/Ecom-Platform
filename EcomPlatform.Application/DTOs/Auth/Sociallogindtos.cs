// ================================================================
// EcomPlatform.Application/DTOs/Auth/SocialLoginDtos.cs
// ================================================================

namespace EcomPlatform.Application.DTOs.Auth
{
    /// <summary>
    /// بيتبعت من الـ Frontend بعد ما Google يرجع idToken
    /// </summary>
    public class GoogleLoginDto
    {
        /// <summary>
        /// الـ ID Token اللي بيجي من Google Sign-In SDK
        /// </summary>
        public string IdToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// بيتبعت من الـ Frontend بعد ما Apple يرجع idToken
    /// ملاحظة: Apple بيبعت الاسم بس في أول مرة تسجيل دخول
    /// </summary>
    public class AppleLoginDto
    {
        /// <summary>
        /// الـ Identity Token اللي بيجي من Sign in with Apple
        /// </summary>
        public string IdToken { get; set; } = string.Empty;

        /// <summary>
        /// بيجي بس في أول مرة — Apple مش بيبعته تاني بعدين
        /// </summary>
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}