namespace EcomPlatform.Application.DTOs.Zatca
{
    public class ZatcaOnboardingRequestDto
    {
        public string CommonName { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public string OrganizationalUnit { get; set; } = string.Empty;
        public string CountryCode { get; set; } = "SA";
        public string VatNumber { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }
}