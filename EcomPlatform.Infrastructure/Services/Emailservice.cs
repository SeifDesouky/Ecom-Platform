using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace EcomPlatform.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        // ─── Core Send Method ───────────────────────────────────────────────
        public async Task SendAsync(string to, string subject, string htmlBody, string? from = null)
        {
            try
            {
                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.EnableSsl,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password)
                };

                var message = new MailMessage
                {
                    From = new MailAddress(from ?? _settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(to);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {To} | Subject: {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} | Subject: {Subject}", to, subject);
                // Don't rethrow — email failure should never crash the main flow
            }
        }

        // ─── Order Confirmation ─────────────────────────────────────────────
        public async Task SendOrderConfirmationAsync(string to, string customerName, string orderNumber, decimal total)
        {
            var subject = $"✅ Order Confirmed — #{orderNumber}";
            var body = BuildLayout(
                title: "Order Confirmed!",
                preheader: $"Your order #{orderNumber} has been received.",
                content: $"""
                    <p>Hi <strong>{customerName}</strong>,</p>
                    <p>Thank you for your order! We've received it and it's being processed.</p>
                    {BuildInfoBox("Order Details", new Dictionary<string, string>
                {
                    ["Order Number"] = $"#{orderNumber}",
                    ["Total Amount"] = $"{total:N2}",
                    ["Status"] = "Processing"
                })}
                    <p>We'll send you another email once your order has been shipped.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Invoice ────────────────────────────────────────────────────────
        public async Task SendInvoiceAsync(string to, string customerName, string invoiceNumber, decimal total, DateTime dueDate)
        {
            var subject = $"📄 Invoice #{invoiceNumber}";
            var body = BuildLayout(
                title: $"Invoice #{invoiceNumber}",
                preheader: $"Your invoice is ready. Amount due: {total:N2}",
                content: $"""
                    <p>Hi <strong>{customerName}</strong>,</p>
                    <p>Please find your invoice details below.</p>
                    {BuildInfoBox("Invoice Details", new Dictionary<string, string>
                {
                    ["Invoice #"] = invoiceNumber,
                    ["Amount Due"] = $"{total:N2}",
                    ["Due Date"] = dueDate.ToString("dd MMM yyyy")
                })}
                    <p>Please ensure payment is made before the due date.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Welcome ────────────────────────────────────────────────────────
        public async Task SendWelcomeAsync(string to, string name, string tenantName)
        {
            var subject = $"🎉 Welcome to {tenantName}!";
            var body = BuildLayout(
                title: $"Welcome to {tenantName}!",
                preheader: "Your account has been created successfully.",
                content: $"""
                    <p>Hi <strong>{name}</strong>,</p>
                    <p>Welcome aboard! Your account on <strong>{tenantName}</strong> has been created successfully.</p>
                    <p>You can now log in and start exploring all the features available to you.</p>
                    <p>If you have any questions, don't hesitate to reach out to our support team.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Password Reset ─────────────────────────────────────────────────
        public async Task SendPasswordResetAsync(string to, string name, string resetLink)
        {
            var subject = "🔑 Reset Your Password";
            var body = BuildLayout(
                title: "Password Reset Request",
                preheader: "Click the link below to reset your password.",
                content: $"""
                    <p>Hi <strong>{name}</strong>,</p>
                    <p>We received a request to reset your password. Click the button below to proceed.</p>
                    <p style="text-align:center; margin: 32px 0;">
                        <a href="{resetLink}" style="background:#4F46E5;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:600;display:inline-block;">
                            Reset Password
                        </a>
                    </p>
                    <p style="color:#6B7280;font-size:14px;">This link will expire in 24 hours. If you didn't request a password reset, you can safely ignore this email.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Domain Verification ────────────────────────────────────────────
        public async Task SendDomainVerificationAsync(string to, string domain, string verificationToken)
        {
            var subject = $"🌐 Verify Your Domain — {domain}";
            var body = BuildLayout(
                title: "Domain Verification",
                preheader: $"Verify your domain {domain} to start using it.",
                content: $"""
                    <p>You've added the domain <strong>{domain}</strong> to your store.</p>
                    <p>To verify ownership, please add the following TXT record to your DNS settings:</p>
                    {BuildInfoBox("DNS Verification Record", new Dictionary<string, string>
                {
                    ["Type"] = "TXT",
                    ["Name"] = "@",
                    ["Value"] = verificationToken
                })}
                    <p style="color:#6B7280;font-size:14px;">DNS changes can take up to 48 hours to propagate. Once verified, your domain will be activated automatically.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Ticket Reply ───────────────────────────────────────────────────
        public async Task SendTicketReplyAsync(string to, string customerName, string ticketSubject, string replyMessage)
        {
            var subject = $"💬 Reply to Your Ticket: {ticketSubject}";
            var body = BuildLayout(
                title: "Support Ticket Update",
                preheader: $"You have a new reply on: {ticketSubject}",
                content: $"""
                    <p>Hi <strong>{customerName}</strong>,</p>
                    <p>Our support team has replied to your ticket: <strong>{ticketSubject}</strong></p>
                    <div style="background:#F9FAFB;border-left:4px solid #4F46E5;padding:16px;border-radius:4px;margin:20px 0;">
                        <p style="margin:0;color:#374151;">{replyMessage}</p>
                    </div>
                    <p>If you have additional questions, please reply to this ticket.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Low Stock Alert ────────────────────────────────────────────────
        public async Task SendLowStockAlertAsync(string to, string productName, int currentStock, int threshold)
        {
            var subject = $"⚠️ Low Stock Alert — {productName}";
            var body = BuildLayout(
                title: "Low Stock Alert",
                preheader: $"{productName} is running low on stock.",
                content: $"""
                    <p>This is an automated alert to inform you that the following product is running low on stock:</p>
                    {BuildInfoBox("Stock Details", new Dictionary<string, string>
                {
                    ["Product"] = productName,
                    ["Current Stock"] = currentStock.ToString(),
                    ["Alert Threshold"] = threshold.ToString()
                })}
                    <p>Please restock this item to avoid losing sales.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Subscription Renewal ───────────────────────────────────────────
        public async Task SendSubscriptionRenewalAsync(string to, string tenantName, string planName, DateTime renewalDate, decimal amount)
        {
            var subject = "🔄 Upcoming Subscription Renewal";
            var body = BuildLayout(
                title: "Subscription Renewal Notice",
                preheader: $"Your {planName} plan renews on {renewalDate:dd MMM yyyy}.",
                content: $"""
                    <p>Hi <strong>{tenantName}</strong>,</p>
                    <p>This is a reminder that your subscription is due for renewal soon.</p>
                    {BuildInfoBox("Renewal Details", new Dictionary<string, string>
                {
                    ["Plan"] = planName,
                    ["Renewal Date"] = renewalDate.ToString("dd MMM yyyy"),
                    ["Amount"] = $"{amount:N2}"
                })}
                    <p>Your plan will auto-renew on the date above. If you wish to make changes, please visit your billing settings.</p>
                """);

            await SendAsync(to, subject, body);
        }

        // ─── Payment Link Created ───────────────────────────────────────────
        public async Task SendPaymentLinkCreatedAsync(
            string to, string tenantName, string linkTitle, string publicUrl, decimal amount)
        {
            var subject = $"تم إنشاء رابط الدفع: {linkTitle}";
            var body = $@"
        <div dir='rtl' style='font-family:Arial;'>
            <h2>مرحباً {tenantName}</h2>
            <p>تم إنشاء رابط دفع جديد بنجاح.</p>
            <table>
                <tr><td><b>العنوان:</b></td><td>{linkTitle}</td></tr>
                <tr><td><b>المبلغ:</b></td><td>{amount:N2} ريال</td></tr>
            </table>
            <p><a href='{publicUrl}' style='background:#007bff;color:#fff;padding:10px 20px;text-decoration:none;border-radius:5px;'>
                عرض الرابط
            </a></p>
        </div>";
            await SendAsync(to, subject, body);
        }

        // ─── Payment Received ───────────────────────────────────────────────
        public async Task SendPaymentReceivedAsync(
            string to, string payerName, string linkTitle, decimal amount, string currency, string? orderNumber)
        {
            var subject = $"تم استلام دفعة: {linkTitle}";
            var orderPart = orderNumber != null
                ? $"<tr><td><b>رقم الطلب:</b></td><td>{orderNumber}</td></tr>"
                : "";
            var body = $@"
        <div dir='rtl' style='font-family:Arial;'>
            <h2>تم استلام الدفعة بنجاح ✓</h2>
            <table>
                <tr><td><b>الدافع:</b></td><td>{payerName}</td></tr>
                <tr><td><b>المبلغ:</b></td><td>{amount:N2} {currency}</td></tr>
                <tr><td><b>الرابط:</b></td><td>{linkTitle}</td></tr>
                {orderPart}
            </table>
        </div>";
            await SendAsync(to, subject, body);
        }

        // ─── HTML Layout Builder ────────────────────────────────────────────
        private static string BuildLayout(string title, string preheader, string content)
        {
            return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>{title}</title>
                    <span style="display:none;max-height:0;overflow:hidden;">{preheader}</span>
                </head>
                <body style="margin:0;padding:0;background:#F3F4F6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
                    <table width="100%" cellpadding="0" cellspacing="0" style="background:#F3F4F6;padding:40px 0;">
                        <tr>
                            <td align="center">
                                <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.1);">
                                    <!-- Header -->
                                    <tr>
                                        <td style="background:linear-gradient(135deg,#4F46E5,#7C3AED);padding:32px;text-align:center;">
                                            <h1 style="margin:0;color:#ffffff;font-size:24px;font-weight:700;">Fatora</h1>
                                            <p style="margin:8px 0 0;color:rgba(255,255,255,0.8);font-size:14px;">Your Commerce Platform</p>
                                        </td>
                                    </tr>
                                    <!-- Content -->
                                    <tr>
                                        <td style="padding:40px 48px;">
                                            <h2 style="margin:0 0 24px;color:#111827;font-size:22px;">{title}</h2>
                                            <div style="color:#374151;font-size:15px;line-height:1.7;">
                                                {content}
                                            </div>
                                        </td>
                                    </tr>
                                    <!-- Footer -->
                                    <tr>
                                        <td style="background:#F9FAFB;padding:24px 48px;border-top:1px solid #E5E7EB;text-align:center;">
                                            <p style="margin:0;color:#9CA3AF;font-size:13px;">
                                                This email was sent by Fatora Platform. 
                                                If you have questions, contact our support team.
                                            </p>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
            """;
        }

        // ─── Info Box Builder ───────────────────────────────────────────────
        private static string BuildInfoBox(string title, Dictionary<string, string> fields)
        {
            var rows = string.Join("", fields.Select(f => $"""
                <tr>
                    <td style="padding:10px 16px;color:#6B7280;font-size:14px;border-bottom:1px solid #F3F4F6;width:40%;">{f.Key}</td>
                    <td style="padding:10px 16px;color:#111827;font-size:14px;font-weight:600;border-bottom:1px solid #F3F4F6;">{f.Value}</td>
                </tr>
            """));

            return $"""
                <div style="background:#F9FAFB;border-radius:8px;overflow:hidden;margin:20px 0;border:1px solid #E5E7EB;">
                    <div style="background:#E5E7EB;padding:10px 16px;">
                        <strong style="color:#374151;font-size:13px;text-transform:uppercase;letter-spacing:0.05em;">{title}</strong>
                    </div>
                    <table width="100%" cellpadding="0" cellspacing="0">
                        {rows}
                    </table>
                </div>
            """;
        }

    }  // ← إقفال كلاس EmailService
}      // ← إقفال الـ namespace