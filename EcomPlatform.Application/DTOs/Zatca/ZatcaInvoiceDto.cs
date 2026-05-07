namespace EcomPlatform.Application.DTOs.Zatca
{
    public class ZatcaInvoiceDto
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string SellerVatNumber { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public decimal SubtotalExVat { get; set; }
        public decimal VatAmount { get; set; }
        public int VatRate { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string XmlContent { get; set; } = string.Empty;
        public string QrCodeBase64 { get; set; } = string.Empty;
    }
}