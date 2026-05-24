// ================================================================
// EcomPlatform.Shared/Settings/SocialAuthSettings.cs
// ================================================================

namespace EcomPlatform.Shared.Settings
{
    public class GoogleAuthSettings
    {
        /// <summary>
        /// من Google Console → APIs & Services → Credentials
        /// </summary>
        public string ClientId { get; set; } = string.Empty;
    }

    public class AppleAuthSettings
    {
        /// <summary>
        /// من Apple Developer → Certificates → Services ID (مثال: com.yourapp.service)
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// الـ Team ID من Apple Developer account
        /// </summary>
        public string TeamId { get; set; } = string.Empty;

        /// <summary>
        /// الـ Key ID من الـ private key اللي أنشأته في Apple Developer
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// محتوى الـ .p8 private key file (بدون السطر الأول والأخير)
        /// يُخزَّن في Environment Variable أو Azure Key Vault — مش في الـ appsettings مباشرة
        /// </summary>
        public string PrivateKey { get; set; } = string.Empty;
    }
}