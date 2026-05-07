using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Zatca;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Interfaces;
using QRCoder;
using System.Text;
using System.Xml.Linq;

namespace EcomPlatform.Infrastructure.Services
{
    public class ZatcaService : IZatcaService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ZatcaService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<ZatcaInvoiceDto>> GenerateZatcaInvoiceAsync(Guid invoiceId)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                return ApiResponse<ZatcaInvoiceDto>.Fail("Invoice not found");

            var items = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == invoiceId);
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(invoice.TenantId);

            decimal vatRate = 0.15m;
            decimal subtotalExVat = invoice.SubTotal / (1 + vatRate);
            decimal vatAmount = invoice.SubTotal - subtotalExVat;
            decimal total = subtotalExVat + vatAmount - invoice.Discount + invoice.Tax;

            var xml = GenerateZatcaXml(invoice, items.ToList(), tenant, subtotalExVat, vatAmount, total);
            var qrCode = GenerateQrCode(invoice, tenant, subtotalExVat, vatAmount);

            var result = new ZatcaInvoiceDto
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.CreatedAt,
                SellerName = tenant?.Name ?? "EcomPlatform",
                SellerVatNumber = "300000000000003",
                BuyerName = invoice.CustomerName,
                SubtotalExVat = Math.Round(subtotalExVat, 2),
                VatAmount = Math.Round(vatAmount, 2),
                VatRate = 15,
                Discount = invoice.Discount,
                Total = Math.Round(total, 2),
                XmlContent = xml,
                QrCodeBase64 = qrCode
            };

            return ApiResponse<ZatcaInvoiceDto>.Ok(result, "ZATCA Invoice generated successfully");
        }

        private static string GenerateZatcaXml(
            Core.Entities.Invoice invoice,
            List<Core.Entities.InvoiceItem> items,
            Core.Entities.Tenant? tenant,
            decimal subtotalExVat,
            decimal vatAmount,
            decimal total)
        {
            XNamespace ns = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
            XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(ns + "Invoice",
                    new XAttribute(XNamespace.Xmlns + "cac", cac),
                    new XAttribute(XNamespace.Xmlns + "cbc", cbc),

                    new XElement(cbc + "ID", invoice.InvoiceNumber),
                    new XElement(cbc + "IssueDate", invoice.CreatedAt.ToString("yyyy-MM-dd")),
                    new XElement(cbc + "IssueTime", invoice.CreatedAt.ToString("HH:mm:ss")),
                    new XElement(cbc + "InvoiceTypeCode", "388"),
                    new XElement(cbc + "DocumentCurrencyCode", "SAR"),

                    new XElement(cac + "AccountingSupplierParty",
                        new XElement(cac + "Party",
                            new XElement(cac + "PartyName",
                                new XElement(cbc + "Name", tenant?.Name ?? "EcomPlatform")),
                            new XElement(cac + "PartyTaxScheme",
                                new XElement(cbc + "CompanyID", "300000000000003"),
                                new XElement(cac + "TaxScheme",
                                    new XElement(cbc + "ID", "VAT"))))),

                    new XElement(cac + "AccountingCustomerParty",
                        new XElement(cac + "Party",
                            new XElement(cac + "PartyName",
                                new XElement(cbc + "Name", invoice.CustomerName)),
                            new XElement(cac + "PostalAddress",
                                new XElement(cbc + "CityName", invoice.CustomerAddress)))),

                    new XElement(cac + "TaxTotal",
                        new XElement(cbc + "TaxAmount",
                            new XAttribute("currencyID", "SAR"),
                            Math.Round(vatAmount, 2).ToString("F2")),
                        new XElement(cac + "TaxSubtotal",
                            new XElement(cbc + "TaxableAmount",
                                new XAttribute("currencyID", "SAR"),
                                Math.Round(subtotalExVat, 2).ToString("F2")),
                            new XElement(cbc + "TaxAmount",
                                new XAttribute("currencyID", "SAR"),
                                Math.Round(vatAmount, 2).ToString("F2")),
                            new XElement(cac + "TaxCategory",
                                new XElement(cbc + "ID", "S"),
                                new XElement(cbc + "Percent", "15"),
                                new XElement(cac + "TaxScheme",
                                    new XElement(cbc + "ID", "VAT"))))),

                    new XElement(cac + "LegalMonetaryTotal",
                        new XElement(cbc + "LineExtensionAmount",
                            new XAttribute("currencyID", "SAR"),
                            Math.Round(subtotalExVat, 2).ToString("F2")),
                        new XElement(cbc + "TaxExclusiveAmount",
                            new XAttribute("currencyID", "SAR"),
                            Math.Round(subtotalExVat, 2).ToString("F2")),
                        new XElement(cbc + "TaxInclusiveAmount",
                            new XAttribute("currencyID", "SAR"),
                            Math.Round(total, 2).ToString("F2")),
                        new XElement(cbc + "AllowanceTotalAmount",
                            new XAttribute("currencyID", "SAR"),
                            invoice.Discount.ToString("F2")),
                        new XElement(cbc + "PayableAmount",
                            new XAttribute("currencyID", "SAR"),
                            Math.Round(total, 2).ToString("F2"))),

                    items.Select((item, index) =>
                        new XElement(cac + "InvoiceLine",
                            new XElement(cbc + "ID", index + 1),
                            new XElement(cbc + "InvoicedQuantity",
                                new XAttribute("unitCode", "PCE"), item.Quantity),
                            new XElement(cbc + "LineExtensionAmount",
                                new XAttribute("currencyID", "SAR"),
                                item.TotalPrice.ToString("F2")),
                            new XElement(cac + "Item",
                                new XElement(cbc + "Name", item.Description)),
                            new XElement(cac + "Price",
                                new XElement(cbc + "PriceAmount",
                                    new XAttribute("currencyID", "SAR"),
                                    item.UnitPrice.ToString("F2")))))
                )
            );

            return xml.ToString();
        }

        private static string GenerateQrCode(
            Core.Entities.Invoice invoice,
            Core.Entities.Tenant? tenant,
            decimal subtotalExVat,
            decimal vatAmount)
        {
            var tlvData = new List<byte>();

            var sellerName = Encoding.UTF8.GetBytes(tenant?.Name ?? "EcomPlatform");
            tlvData.Add(1);
            tlvData.Add((byte)sellerName.Length);
            tlvData.AddRange(sellerName);

            var vatNumber = Encoding.UTF8.GetBytes("300000000000003");
            tlvData.Add(2);
            tlvData.Add((byte)vatNumber.Length);
            tlvData.AddRange(vatNumber);

            var invoiceDate = Encoding.UTF8.GetBytes(invoice.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"));
            tlvData.Add(3);
            tlvData.Add((byte)invoiceDate.Length);
            tlvData.AddRange(invoiceDate);

            var totalWithVat = Encoding.UTF8.GetBytes((subtotalExVat + vatAmount).ToString("F2"));
            tlvData.Add(4);
            tlvData.Add((byte)totalWithVat.Length);
            tlvData.AddRange(totalWithVat);

            var vatAmountBytes = Encoding.UTF8.GetBytes(vatAmount.ToString("F2"));
            tlvData.Add(5);
            tlvData.Add((byte)vatAmountBytes.Length);
            tlvData.AddRange(vatAmountBytes);

            var tlvBase64 = Convert.ToBase64String(tlvData.ToArray());

            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(tlvBase64, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(10);

            return Convert.ToBase64String(qrCodeBytes);
        }
    }
}