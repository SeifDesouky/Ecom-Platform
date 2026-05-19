namespace EcomPlatform.Application.DTOs.Zatca
{
    public class ZatcaOnboardingDto
    {
        public string Certificate { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
    }
}