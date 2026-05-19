using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Zatca;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Interfaces;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using QRCoder;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;

namespace EcomPlatform.Infrastructure.Services
{
    public class ZatcaService : IZatcaService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly HttpClient _httpClient;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        private string SandboxBaseUrl => _configuration["Zatca:SandboxBaseUrl"]
            ?? "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal";
        private string SandboxCertificate => _configuration["Zatca:SandboxCertificate"] ?? "";
        private string SandboxPrivateKey => _configuration["Zatca:SandboxPrivateKey"] ?? "";

        public ZatcaService(
            IUnitOfWork unitOfWork,
            IHttpClientFactory httpClientFactory,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _httpClient = httpClientFactory.CreateClient("ZatcaClient");
            _configuration = configuration;
        }

        // ── Generate CSR ────────────────────────────────────────────────────

        public ZatcaCsrDto GenerateCsr(string commonName, string organizationName,
            string organizationalUnit, string countryCode, string vatNumber)
        {
            // 1. Generate EC Key Pair
            var keyGen = new ECKeyPairGenerator("EC");
            var secureRandom = new SecureRandom();
            keyGen.Init(new Org.BouncyCastle.Crypto.Parameters.ECKeyGenerationParameters(
                Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetOid("secp256k1"),
                secureRandom));
            var keyPair = keyGen.GenerateKeyPair();

            // 2. Build Subject
            var subject = new X509Name(new[]
            {
                X509Name.CN, X509Name.O, X509Name.OU, X509Name.C,
                new Org.BouncyCastle.Asn1.DerObjectIdentifier("2.5.4.97")
            }, new[]
            {
                commonName, organizationName, organizationalUnit,
                countryCode, $"1-EcomPlatform|2-Fatora|3-{vatNumber}"
            });

            // 3. Build CSR
            var signatureFactory = new Asn1SignatureFactory("SHA256withECDSA",
                keyPair.Private, secureRandom);
            var csr = new Pkcs10CertificationRequest(signatureFactory, subject,
                keyPair.Public, null);

            // 4. Export CSR as PEM
            var csrPem = new StringBuilder();
            using (var sw = new System.IO.StringWriter(csrPem))
            {
                var pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(csr);
            }

            // 5. Export Private Key as PEM
            var privateKeyPem = new StringBuilder();
            using (var sw = new System.IO.StringWriter(privateKeyPem))
            {
                var pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(keyPair.Private);
            }

            return new ZatcaCsrDto
            {
                Csr = csrPem.ToString(),
                PrivateKey = privateKeyPem.ToString()
            };
        }

        public async Task<ApiResponse<ZatcaOnboardingDto>> OnboardAsync(ZatcaOnboardingRequestDto request)
        {
            try
            {
                var csrResult = GenerateCsr(
                    request.CommonName,
                    request.OrganizationName,
                    request.OrganizationalUnit,
                    request.CountryCode,
                    request.VatNumber);

                var csrBase64 = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(csrResult.Csr));

                var payload = new { csr = csrBase64 };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
                _httpClient.DefaultRequestHeaders.Add("OTP", request.Otp);

                var response = await _httpClient.PostAsync(
                    $"{SandboxBaseUrl}/compliance", content);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var zatcaResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

                    var certificate = zatcaResponse.TryGetProperty("binarySecurityToken", out var cert)
                        ? cert.GetString() ?? ""
                        : "";

                    var secret = zatcaResponse.TryGetProperty("secret", out var s)
                        ? s.GetString() ?? ""
                        : "";

                    return ApiResponse<ZatcaOnboardingDto>.Ok(new ZatcaOnboardingDto
                    {
                        Certificate = certificate,
                        PrivateKey = csrResult.PrivateKey,
                        Secret = secret,
                        RequestId = zatcaResponse.TryGetProperty("requestID", out var rid)
                            ? rid.GetString() ?? ""
                            : ""
                    }, "Onboarding successful — احفظ الـ Certificate و PrivateKey في الـ appsettings");
                }
                else
                {
                    return ApiResponse<ZatcaOnboardingDto>.Fail(
                        $"ZATCA Onboarding failed: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<ZatcaOnboardingDto>.Fail(
                    $"Onboarding error: {ex.Message}");
            }
        }

        // ── Generate ZATCA Invoice ───────────────────────────────────────────

        public async Task<ApiResponse<ZatcaInvoiceDto>> GenerateZatcaInvoiceAsync(Guid invoiceId)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                return ApiResponse<ZatcaInvoiceDto>.Fail("Invoice not found");

            var itemsResult = await _unitOfWork.InvoiceItems.FindAsync(i => i.InvoiceId == invoiceId);
            var items = itemsResult?.ToList() ?? new List<Core.Entities.InvoiceItem>();

            var tenant = invoice.TenantId.HasValue
                ? await _unitOfWork.Tenants.GetByIdAsync(invoice.TenantId.Value)
                : null;

            decimal vatRate = tenant?.VatRate ?? 0.15m;
            decimal subtotalExVat = invoice.SubTotal;
            decimal vatAmount = Math.Round(subtotalExVat * vatRate, 2);
            decimal total = subtotalExVat + vatAmount - invoice.Discount;

            var xml = GenerateZatcaXml(invoice, items, tenant, subtotalExVat, vatAmount, total);
            var qrCode = GenerateQrCode(invoice, tenant, subtotalExVat, vatAmount);

            var result = new ZatcaInvoiceDto
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.CreatedAt,
                SellerName = tenant?.Name ?? "EcomPlatform",
                SellerVatNumber = tenant?.VatNumber ?? "300000000000003",
                BuyerName = invoice.CustomerName,
                SubtotalExVat = Math.Round(subtotalExVat, 2),
                VatAmount = Math.Round(vatAmount, 2),
                VatRate = (int)(vatRate * 100),
                Discount = invoice.Discount,
                Total = Math.Round(total, 2),
                XmlContent = xml,
                QrCodeBase64 = qrCode
            };

            return ApiResponse<ZatcaInvoiceDto>.Ok(result, "ZATCA Invoice generated successfully");
        }

        // ── Submit Invoice ───────────────────────────────────────────────────

        public async Task<ApiResponse<ZatcaSubmissionDto>> SubmitInvoiceAsync(Guid invoiceId)
        {
            var invoiceResult = await GenerateZatcaInvoiceAsync(invoiceId);
            if (!invoiceResult.Success)
                return ApiResponse<ZatcaSubmissionDto>.Fail(invoiceResult.Message);

            var invoice = invoiceResult.Data!;

            try
            {
                var xmlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(invoice.XmlContent));
                var xmlHash = Convert.ToBase64String(
                    System.Security.Cryptography.SHA256.HashData(
                        Encoding.UTF8.GetBytes(invoice.XmlContent)));

                var payload = new
                {
                    invoiceHash = xmlHash,
                    uuid = Guid.NewGuid().ToString(),
                    invoice = xmlBase64
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{SandboxCertificate}:{SandboxPrivateKey}"));

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
                _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en");

                var response = await _httpClient.PostAsync(
                    $"{SandboxBaseUrl}/invoices/reporting/single", content);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var zatcaResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

                    return ApiResponse<ZatcaSubmissionDto>.Ok(new ZatcaSubmissionDto
                    {
                        InvoiceNumber = invoice.InvoiceNumber,
                        Status = "REPORTED",
                        ReportingStatus = zatcaResponse.TryGetProperty("reportingStatus", out var rs)
                            ? rs.GetString() ?? "REPORTED" : "REPORTED",
                        QrCodeBase64 = invoice.QrCodeBase64,
                        XmlContent = invoice.XmlContent,
                        WarningMessages = zatcaResponse.TryGetProperty("warningMessages", out var wm)
                            ? wm.ToString() : string.Empty,
                        SubmittedAt = DateTime.UtcNow
                    }, "Invoice submitted to ZATCA successfully");
                }
                else
                {
                    return ApiResponse<ZatcaSubmissionDto>.Fail(
                        $"ZATCA rejected: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<ZatcaSubmissionDto>.Fail($"Submit failed: {ex.Message}");
            }
        }

        // ── Check Compliance ─────────────────────────────────────────────────

        public async Task<ApiResponse<ZatcaSubmissionDto>> CheckComplianceAsync(Guid invoiceId)
        {
            var invoiceResult = await GenerateZatcaInvoiceAsync(invoiceId);
            if (!invoiceResult.Success)
                return ApiResponse<ZatcaSubmissionDto>.Fail(invoiceResult.Message);

            var invoice = invoiceResult.Data!;

            try
            {
                var xmlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(invoice.XmlContent));
                var xmlHash = Convert.ToBase64String(
                    System.Security.Cryptography.SHA256.HashData(
                        Encoding.UTF8.GetBytes(invoice.XmlContent)));

                var payload = new
                {
                    invoiceHash = xmlHash,
                    uuid = Guid.NewGuid().ToString(),
                    invoice = xmlBase64
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{SandboxCertificate}:{SandboxPrivateKey}"));

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
                _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en");

                var response = await _httpClient.PostAsync(
                    $"{SandboxBaseUrl}/compliance/invoices", content);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var zatcaResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

                    return ApiResponse<ZatcaSubmissionDto>.Ok(new ZatcaSubmissionDto
                    {
                        InvoiceNumber = invoice.InvoiceNumber,
                        Status = "COMPLIANT",
                        ClearanceStatus = zatcaResponse.TryGetProperty("clearanceStatus", out var cs)
                            ? cs.GetString() ?? "COMPLIANT" : "COMPLIANT",
                        QrCodeBase64 = invoice.QrCodeBase64,
                        XmlContent = invoice.XmlContent,
                        WarningMessages = zatcaResponse.TryGetProperty("warningMessages", out var wm)
                            ? wm.ToString() : string.Empty,
                        SubmittedAt = DateTime.UtcNow
                    }, "Compliance check passed");
                }
                else
                {
                    return ApiResponse<ZatcaSubmissionDto>.Fail(
                        $"Compliance failed: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<ZatcaSubmissionDto>.Fail($"Compliance error: {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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
                                new XElement(cbc + "CompanyID", tenant?.VatNumber ?? "300000000000003"),
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
            tlvData.Add(1); tlvData.Add((byte)sellerName.Length); tlvData.AddRange(sellerName);

            var vatNumber = Encoding.UTF8.GetBytes(tenant?.VatNumber ?? "300000000000003");
            tlvData.Add(2); tlvData.Add((byte)vatNumber.Length); tlvData.AddRange(vatNumber);

            var invoiceDate = Encoding.UTF8.GetBytes(invoice.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"));
            tlvData.Add(3); tlvData.Add((byte)invoiceDate.Length); tlvData.AddRange(invoiceDate);

            var totalWithVat = Encoding.UTF8.GetBytes(Math.Round(subtotalExVat + vatAmount, 2).ToString("F2"));
            tlvData.Add(4); tlvData.Add((byte)totalWithVat.Length); tlvData.AddRange(totalWithVat);

            var vatAmountBytes = Encoding.UTF8.GetBytes(Math.Round(vatAmount, 2).ToString("F2"));
            tlvData.Add(5); tlvData.Add((byte)vatAmountBytes.Length); tlvData.AddRange(vatAmountBytes);

            var tlvBase64 = Convert.ToBase64String(tlvData.ToArray());

            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(tlvBase64, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(10);

            return Convert.ToBase64String(qrCodeBytes);
        }
    }
}