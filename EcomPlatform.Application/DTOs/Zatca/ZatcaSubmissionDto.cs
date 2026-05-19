namespace EcomPlatform.Application.DTOs.Zatca
{
    public class ZatcaSubmissionDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ReportingStatus { get; set; } = string.Empty;
        public string ClearanceStatus { get; set; } = string.Empty;
        public string QrCodeBase64 { get; set; } = string.Empty;
        public string XmlContent { get; set; } = string.Empty;
        public string WarningMessages { get; set; } = string.Empty;
        public string ErrorMessages { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}