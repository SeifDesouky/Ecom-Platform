namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody, string? from = null);
        Task SendOrderConfirmationAsync(string to, string customerName, string orderNumber, decimal total);
        Task SendInvoiceAsync(string to, string customerName, string invoiceNumber, decimal total, DateTime dueDate);
        Task SendWelcomeAsync(string to, string name, string tenantName);
        Task SendPasswordResetAsync(string to, string name, string resetLink);
        Task SendDomainVerificationAsync(string to, string domain, string verificationToken);
        Task SendTicketReplyAsync(string to, string customerName, string ticketSubject, string replyMessage);
        Task SendLowStockAlertAsync(string to, string productName, int currentStock, int threshold);
        Task SendSubscriptionRenewalAsync(string to, string tenantName, string planName, DateTime renewalDate, decimal amount);
    }
}