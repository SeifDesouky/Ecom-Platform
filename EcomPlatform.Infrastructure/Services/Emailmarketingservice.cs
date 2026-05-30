using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.EmailMarketing;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EcomPlatform.Infrastructure.Services
{
    public class EmailMarketingService : IEmailMarketingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailMarketingService> _logger;

        public EmailMarketingService(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            ILogger<EmailMarketingService> logger)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════
        // MAILING LISTS
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<MailingListResponseDto>> CreateListAsync(CreateMailingListDto dto)
        {
            var duplicate = await _unitOfWork.MailingLists.FindAsync(l =>
                l.TenantId == dto.TenantId &&
                l.Name == dto.Name.Trim());

            if (duplicate.Any())
                return ApiResponse<MailingListResponseDto>.Fail(
                    "A mailing list with this name already exists.");

            var list = new MailingList
            {
                TenantId = dto.TenantId,
                Name = dto.Name.Trim(),
                Description = dto.Description.Trim(),
                WelcomeEmailSubject = dto.WelcomeEmailSubject,
                WelcomeEmailBody = dto.WelcomeEmailBody,
                IsActive = true
            };

            await _unitOfWork.MailingLists.AddAsync(list);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<MailingListResponseDto>.Ok(
                MapListToDto(list, 0, 0), "Mailing list created.");
        }

        public async Task<ApiResponse<MailingListResponseDto>> UpdateListAsync(
            Guid id, UpdateMailingListDto dto)
        {
            var list = await _unitOfWork.MailingLists.GetByIdAsync(id);
            if (list == null)
                return ApiResponse<MailingListResponseDto>.Fail("Mailing list not found.");

            list.Name = dto.Name.Trim();
            list.Description = dto.Description.Trim();
            list.IsActive = dto.IsActive;
            list.WelcomeEmailSubject = dto.WelcomeEmailSubject;
            list.WelcomeEmailBody = dto.WelcomeEmailBody;

            await _unitOfWork.MailingLists.UpdateAsync(list);
            await _unitOfWork.SaveChangesAsync();

            var counts = await GetListCountsAsync(id);
            return ApiResponse<MailingListResponseDto>.Ok(
                MapListToDto(list, counts.total, counts.active), "Mailing list updated.");
        }

        public async Task<ApiResponse<bool>> DeleteListAsync(Guid id)
        {
            var list = await _unitOfWork.MailingLists.GetByIdAsync(id);
            if (list == null)
                return ApiResponse<bool>.Fail("Mailing list not found.");

            await _unitOfWork.MailingLists.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Mailing list deleted.");
        }

        public async Task<ApiResponse<MailingListResponseDto>> GetListByIdAsync(Guid id)
        {
            var list = await _unitOfWork.MailingLists.GetByIdAsync(id);
            if (list == null)
                return ApiResponse<MailingListResponseDto>.Fail("Mailing list not found.");

            var counts = await GetListCountsAsync(id);
            return ApiResponse<MailingListResponseDto>.Ok(
                MapListToDto(list, counts.total, counts.active));
        }

        public async Task<ApiResponse<PagedResponse<MailingListResponseDto>>> GetListsAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.MailingLists.GetPagedAsync(
                l => l.TenantId == tenantId,
                pagination.Skip,
                pagination.PageSize);

            var dtos = new List<MailingListResponseDto>();
            foreach (var l in items.OrderByDescending(x => x.CreatedAt))
            {
                var counts = await GetListCountsAsync(l.Id);
                dtos.Add(MapListToDto(l, counts.total, counts.active));
            }

            return ApiResponse<PagedResponse<MailingListResponseDto>>.Ok(
                PagedResponse<MailingListResponseDto>.Create(dtos, total, pagination));
        }

        // ════════════════════════════════════════════════════════════════
        // SUBSCRIBERS
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<SubscriberResponseDto>> AddSubscriberAsync(AddSubscriberDto dto)
        {
            var list = await _unitOfWork.MailingLists.GetByIdAsync(dto.MailingListId);
            if (list == null)
                return ApiResponse<SubscriberResponseDto>.Fail("Mailing list not found.");

            // منع التكرار
            var existing = await _unitOfWork.MailingListSubscribers.FindAsync(s =>
                s.MailingListId == dto.MailingListId &&
                s.Email == dto.Email.ToLower().Trim());

            if (existing.Any())
                return ApiResponse<SubscriberResponseDto>.Fail(
                    "This email is already subscribed to this list.");

            var subscriber = new MailingListSubscriber
            {
                TenantId = dto.TenantId,
                MailingListId = dto.MailingListId,
                Email = dto.Email.ToLower().Trim(),
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Phone = dto.Phone.Trim(),
                CustomerId = dto.CustomerId,
                Source = dto.Source,
                Status = SubscriberStatus.Active
            };

            await _unitOfWork.MailingListSubscribers.AddAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            // Welcome Email
            if (!string.IsNullOrWhiteSpace(list.WelcomeEmailSubject) &&
                !string.IsNullOrWhiteSpace(list.WelcomeEmailBody))
            {
                var personalizedBody = list.WelcomeEmailBody
                    .Replace("{{FirstName}}", subscriber.FirstName)
                    .Replace("{{LastName}}", subscriber.LastName)
                    .Replace("{{Email}}", subscriber.Email)
                    .Replace("{{UnsubscribeToken}}", subscriber.UnsubscribeToken);

                _ = _emailService.SendAsync(
                    subscriber.Email,
                    list.WelcomeEmailSubject,
                    personalizedBody);
            }

            return ApiResponse<SubscriberResponseDto>.Ok(
                MapSubscriberToDto(subscriber, list.Name), "Subscriber added successfully.");
        }

        public async Task<ApiResponse<ImportResultDto>> ImportSubscribersAsync(ImportSubscribersDto dto)
        {
            var list = await _unitOfWork.MailingLists.GetByIdAsync(dto.MailingListId);
            if (list == null)
                return ApiResponse<ImportResultDto>.Fail("Mailing list not found.");

            // جيب الإيميلات الموجودة دفعة واحدة
            var existing = (await _unitOfWork.MailingListSubscribers.FindAsync(s =>
                s.MailingListId == dto.MailingListId))
                .Select(s => s.Email)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = new ImportResultDto();

            foreach (var item in dto.Subscribers)
            {
                var email = item.Email.ToLower().Trim();
                if (string.IsNullOrWhiteSpace(email))
                {
                    result.Failed++;
                    result.Errors.Add($"Empty email skipped.");
                    continue;
                }

                if (existing.Contains(email))
                {
                    result.Skipped++;
                    continue;
                }

                try
                {
                    var subscriber = new MailingListSubscriber
                    {
                        TenantId = dto.TenantId,
                        MailingListId = dto.MailingListId,
                        Email = email,
                        FirstName = item.FirstName.Trim(),
                        LastName = item.LastName.Trim(),
                        Phone = item.Phone.Trim(),
                        CustomerId = item.CustomerId,
                        Source = dto.Source,
                        Status = SubscriberStatus.Active
                    };

                    await _unitOfWork.MailingListSubscribers.AddAsync(subscriber);
                    existing.Add(email);
                    result.Added++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"{email}: {ex.Message}");
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ImportResultDto>.Ok(result,
                $"Import complete: {result.Added} added, {result.Skipped} skipped, {result.Failed} failed.");
        }

        public async Task<ApiResponse<bool>> UnsubscribeByTokenAsync(string token)
        {
            var subs = await _unitOfWork.MailingListSubscribers.FindAsync(
                s => s.UnsubscribeToken == token);

            var subscriber = subs.FirstOrDefault();
            if (subscriber == null)
                return ApiResponse<bool>.Fail("Invalid unsubscribe link.");

            subscriber.Status = SubscriberStatus.Unsubscribed;
            subscriber.UnsubscribedAt = DateTime.UtcNow;

            await _unitOfWork.MailingListSubscribers.UpdateAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "You have been unsubscribed successfully.");
        }

        public async Task<ApiResponse<bool>> UnsubscribeAsync(Guid subscriberId)
        {
            var subscriber = await _unitOfWork.MailingListSubscribers.GetByIdAsync(subscriberId);
            if (subscriber == null)
                return ApiResponse<bool>.Fail("Subscriber not found.");

            subscriber.Status = SubscriberStatus.Unsubscribed;
            subscriber.UnsubscribedAt = DateTime.UtcNow;

            await _unitOfWork.MailingListSubscribers.UpdateAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true);
        }

        public async Task<ApiResponse<PagedResponse<SubscriberResponseDto>>> GetSubscribersAsync(
            Guid mailingListId, PaginationParams pagination)
        {
            var list = await _unitOfWork.MailingLists.GetByIdAsync(mailingListId);
            var listName = list?.Name ?? string.Empty;

            var (items, total) = await _unitOfWork.MailingListSubscribers.GetPagedAsync(
                s => s.MailingListId == mailingListId,
                pagination.Skip,
                pagination.PageSize);

            var dtos = items
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => MapSubscriberToDto(s, listName))
                .ToList();

            return ApiResponse<PagedResponse<SubscriberResponseDto>>.Ok(
                PagedResponse<SubscriberResponseDto>.Create(dtos, total, pagination));
        }

        public async Task<ApiResponse<bool>> DeleteSubscriberAsync(Guid subscriberId)
        {
            var sub = await _unitOfWork.MailingListSubscribers.GetByIdAsync(subscriberId);
            if (sub == null)
                return ApiResponse<bool>.Fail("Subscriber not found.");

            await _unitOfWork.MailingListSubscribers.DeleteAsync(subscriberId);
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true);
        }

        // ════════════════════════════════════════════════════════════════
        // CAMPAIGNS
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<CampaignResponseDto>> CreateCampaignAsync(CreateCampaignDto dto)
        {
            var campaign = new Campaign
            {
                TenantId = dto.TenantId,
                Name = dto.Name.Trim(),
                Subject = dto.Subject.Trim(),
                PreviewText = dto.PreviewText.Trim(),
                FromName = dto.FromName.Trim(),
                FromEmail = dto.FromEmail.Trim(),
                HtmlBody = dto.HtmlBody,
                TextBody = dto.TextBody,
                ScheduledAt = dto.ScheduledAt,
                Status = dto.ScheduledAt.HasValue
                                  ? CampaignStatus.Scheduled
                                  : CampaignStatus.Draft
            };

            await _unitOfWork.Campaigns.AddAsync(campaign);
            await _unitOfWork.SaveChangesAsync();

            // ربط القوائم
            var listNames = new List<string>();
            foreach (var listId in dto.MailingListIds.Distinct())
            {
                var ml = await _unitOfWork.MailingLists.GetByIdAsync(listId);
                if (ml == null) continue;

                var link = new CampaignMailingList
                {
                    CampaignId = campaign.Id,
                    MailingListId = listId
                };
                await _unitOfWork.CampaignMailingLists.AddAsync(link);
                listNames.Add(ml.Name);
            }

            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<CampaignResponseDto>.Ok(
                MapCampaignToDto(campaign, listNames), "Campaign created.");
        }

        public async Task<ApiResponse<CampaignResponseDto>> UpdateCampaignAsync(
            Guid id, UpdateCampaignDto dto)
        {
            var campaign = await _unitOfWork.Campaigns.GetByIdAsync(id);
            if (campaign == null)
                return ApiResponse<CampaignResponseDto>.Fail("Campaign not found.");

            if (campaign.Status == CampaignStatus.Sent ||
                campaign.Status == CampaignStatus.Sending)
                return ApiResponse<CampaignResponseDto>.Fail(
                    "Cannot edit a campaign that has already been sent.");

            campaign.Name = dto.Name.Trim();
            campaign.Subject = dto.Subject.Trim();
            campaign.PreviewText = dto.PreviewText.Trim();
            campaign.FromName = dto.FromName.Trim();
            campaign.FromEmail = dto.FromEmail.Trim();
            campaign.HtmlBody = dto.HtmlBody;
            campaign.TextBody = dto.TextBody;
            campaign.ScheduledAt = dto.ScheduledAt;
            campaign.Status = dto.ScheduledAt.HasValue
                                       ? CampaignStatus.Scheduled
                                       : CampaignStatus.Draft;

            await _unitOfWork.Campaigns.UpdateAsync(campaign);

            // أعد ربط القوائم — امسح القديمة وأضف الجديدة
            var oldLinks = await _unitOfWork.CampaignMailingLists.FindAsync(
                l => l.CampaignId == id);
            foreach (var link in oldLinks)
                await _unitOfWork.CampaignMailingLists.DeleteAsync(link.Id);

            var listNames = new List<string>();
            foreach (var listId in dto.MailingListIds.Distinct())
            {
                var ml = await _unitOfWork.MailingLists.GetByIdAsync(listId);
                if (ml == null) continue;

                await _unitOfWork.CampaignMailingLists.AddAsync(new CampaignMailingList
                {
                    CampaignId = id,
                    MailingListId = listId
                });
                listNames.Add(ml.Name);
            }

            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<CampaignResponseDto>.Ok(
                MapCampaignToDto(campaign, listNames), "Campaign updated.");
        }

        public async Task<ApiResponse<bool>> DeleteCampaignAsync(Guid id)
        {
            var campaign = await _unitOfWork.Campaigns.GetByIdAsync(id);
            if (campaign == null)
                return ApiResponse<bool>.Fail("Campaign not found.");

            if (campaign.Status == CampaignStatus.Sending)
                return ApiResponse<bool>.Fail("Cannot delete a campaign while it's being sent.");

            await _unitOfWork.Campaigns.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true);
        }

        public async Task<ApiResponse<CampaignResponseDto>> GetCampaignByIdAsync(Guid id)
        {
            var campaign = await _unitOfWork.Campaigns.GetByIdAsync(id);
            if (campaign == null)
                return ApiResponse<CampaignResponseDto>.Fail("Campaign not found.");

            var listNames = await GetCampaignListNamesAsync(id);
            return ApiResponse<CampaignResponseDto>.Ok(MapCampaignToDto(campaign, listNames));
        }

        public async Task<ApiResponse<PagedResponse<CampaignResponseDto>>> GetCampaignsAsync(
            Guid tenantId, PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.Campaigns.GetPagedAsync(
                c => c.TenantId == tenantId,
                pagination.Skip,
                pagination.PageSize);

            var dtos = new List<CampaignResponseDto>();
            foreach (var c in items.OrderByDescending(x => x.CreatedAt))
            {
                var listNames = await GetCampaignListNamesAsync(c.Id);
                dtos.Add(MapCampaignToDto(c, listNames));
            }

            return ApiResponse<PagedResponse<CampaignResponseDto>>.Ok(
                PagedResponse<CampaignResponseDto>.Create(dtos, total, pagination));
        }

        public async Task<ApiResponse<CampaignResponseDto>> SendCampaignAsync(Guid campaignId)
        {
            var campaign = await _unitOfWork.Campaigns.GetByIdAsync(campaignId);
            if (campaign == null)
                return ApiResponse<CampaignResponseDto>.Fail("Campaign not found.");

            if (campaign.Status == CampaignStatus.Sent ||
                campaign.Status == CampaignStatus.Sending)
                return ApiResponse<CampaignResponseDto>.Fail("Campaign already sent.");

            if (campaign.Status == CampaignStatus.Cancelled)
                return ApiResponse<CampaignResponseDto>.Fail("Campaign was cancelled.");

            if (string.IsNullOrWhiteSpace(campaign.HtmlBody))
                return ApiResponse<CampaignResponseDto>.Fail("Campaign has no email body.");

            // جيب المشتركين من كل القوائم المرتبطة
            var links = await _unitOfWork.CampaignMailingLists.FindAsync(
                l => l.CampaignId == campaignId);

            var listIds = links.Select(l => l.MailingListId).ToList();
            var allSubs = new List<MailingListSubscriber>();

            foreach (var listId in listIds)
            {
                var subs = await _unitOfWork.MailingListSubscribers.FindAsync(s =>
                    s.MailingListId == listId &&
                    s.Status == SubscriberStatus.Active);
                allSubs.AddRange(subs);
            }

            // إزالة التكرارات بالإيميل
            var uniqueSubs = allSubs
                .GroupBy(s => s.Email.ToLower())
                .Select(g => g.First())
                .ToList();

            if (!uniqueSubs.Any())
                return ApiResponse<CampaignResponseDto>.Fail("No active subscribers in selected lists.");

            // حدّث الحالة لـ Sending
            campaign.Status = CampaignStatus.Sending;
            campaign.TotalRecipients = uniqueSubs.Count;
            await _unitOfWork.Campaigns.UpdateAsync(campaign);
            await _unitOfWork.SaveChangesAsync();

            // ابدأ الإرسال — Fire and Forget مع logging
            _ = Task.Run(async () =>
            {
                int sent = 0, failed = 0;

                foreach (var sub in uniqueSubs)
                {
                    // أضف tracking pixel + unsubscribe link
                    var recipient = new CampaignRecipient
                    {
                        CampaignId = campaignId,
                        Email = sub.Email,
                        Name = $"{sub.FirstName} {sub.LastName}".Trim(),
                        Status = CampaignRecipientStatus.Pending
                    };

                    await _unitOfWork.CampaignRecipients.AddAsync(recipient);
                    await _unitOfWork.SaveChangesAsync();

                    var personalizedHtml = campaign.HtmlBody
                        .Replace("{{FirstName}}", sub.FirstName)
                        .Replace("{{LastName}}", sub.LastName)
                        .Replace("{{Email}}", sub.Email)
                        .Replace("{{UnsubscribeToken}}", sub.UnsubscribeToken)
                        .Replace("{{TrackingToken}}", recipient.TrackingToken);

                    try
                    {
                        await _emailService.SendAsync(
                            sub.Email,
                            campaign.Subject,
                            personalizedHtml,
                            campaign.FromEmail);

                        recipient.Status = CampaignRecipientStatus.Sent;
                        recipient.SentAt = DateTime.UtcNow;
                        sent++;
                    }
                    catch (Exception ex)
                    {
                        recipient.Status = CampaignRecipientStatus.Bounced;
                        recipient.BouncedAt = DateTime.UtcNow;
                        recipient.FailReason = ex.Message;
                        failed++;
                        _logger.LogWarning("Failed to send campaign {Id} to {Email}: {Err}",
                            campaignId, sub.Email, ex.Message);
                    }

                    await _unitOfWork.CampaignRecipients.UpdateAsync(recipient);
                    await _unitOfWork.SaveChangesAsync();
                }

                // تحديث الحالة النهائية
                campaign.Status = CampaignStatus.Sent;
                campaign.SentAt = DateTime.UtcNow;
                campaign.SentCount = sent;
                campaign.BouncedCount = failed;
                await _unitOfWork.Campaigns.UpdateAsync(campaign);
                await _unitOfWork.SaveChangesAsync();
            });

            var listNames = await GetCampaignListNamesAsync(campaignId);
            return ApiResponse<CampaignResponseDto>.Ok(
                MapCampaignToDto(campaign, listNames),
                $"Campaign is being sent to {uniqueSubs.Count} subscribers.");
        }

        public async Task<ApiResponse<bool>> CancelCampaignAsync(Guid campaignId)
        {
            var campaign = await _unitOfWork.Campaigns.GetByIdAsync(campaignId);
            if (campaign == null)
                return ApiResponse<bool>.Fail("Campaign not found.");

            if (campaign.Status == CampaignStatus.Sent ||
                campaign.Status == CampaignStatus.Sending)
                return ApiResponse<bool>.Fail("Cannot cancel a campaign that is already sent or sending.");

            campaign.Status = CampaignStatus.Cancelled;
            await _unitOfWork.Campaigns.UpdateAsync(campaign);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Campaign cancelled.");
        }

        // ════════════════════════════════════════════════════════════════
        // TRACKING
        // ════════════════════════════════════════════════════════════════

        public async Task TrackOpenAsync(string trackingToken)
        {
            var recipients = await _unitOfWork.CampaignRecipients.FindAsync(
                r => r.TrackingToken == trackingToken);

            var recipient = recipients.FirstOrDefault();
            if (recipient == null || recipient.OpenedAt.HasValue) return;

            recipient.Status = CampaignRecipientStatus.Opened;
            recipient.OpenedAt = DateTime.UtcNow;
            await _unitOfWork.CampaignRecipients.UpdateAsync(recipient);

            // حدّث عداد الحملة
            var campaign = await _unitOfWork.Campaigns.GetByIdAsync(recipient.CampaignId);
            if (campaign != null)
            {
                campaign.OpenedCount++;
                await _unitOfWork.Campaigns.UpdateAsync(campaign);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task TrackClickAsync(string trackingToken, string url)
        {
            var recipients = await _unitOfWork.CampaignRecipients.FindAsync(
                r => r.TrackingToken == trackingToken);

            var recipient = recipients.FirstOrDefault();
            if (recipient == null) return;

            if (!recipient.ClickedAt.HasValue)
            {
                recipient.Status = CampaignRecipientStatus.Clicked;
                recipient.ClickedAt = DateTime.UtcNow;
                await _unitOfWork.CampaignRecipients.UpdateAsync(recipient);

                var campaign = await _unitOfWork.Campaigns.GetByIdAsync(recipient.CampaignId);
                if (campaign != null)
                {
                    campaign.ClickedCount++;
                    await _unitOfWork.Campaigns.UpdateAsync(campaign);
                }

                await _unitOfWork.SaveChangesAsync();
            }
        }

        // ════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════

        private async Task<(int total, int active)> GetListCountsAsync(Guid listId)
        {
            var subs = await _unitOfWork.MailingListSubscribers.FindAsync(
                s => s.MailingListId == listId);
            var total = subs.Count();
            var active = subs.Count(s => s.Status == SubscriberStatus.Active);
            return (total, active);
        }

        private async Task<List<string>> GetCampaignListNamesAsync(Guid campaignId)
        {
            var links = await _unitOfWork.CampaignMailingLists.FindAsync(
                l => l.CampaignId == campaignId);

            var names = new List<string>();
            foreach (var l in links)
            {
                var ml = await _unitOfWork.MailingLists.GetByIdAsync(l.MailingListId);
                if (ml != null) names.Add(ml.Name);
            }
            return names;
        }

        private static MailingListResponseDto MapListToDto(
            MailingList list, int total, int active) => new()
            {
                Id = list.Id,
                Name = list.Name,
                Description = list.Description,
                IsActive = list.IsActive,
                SubscriberCount = total,
                ActiveCount = active,
                WelcomeEmailSubject = list.WelcomeEmailSubject,
                CreatedAt = list.CreatedAt
            };

        private static SubscriberResponseDto MapSubscriberToDto(
            MailingListSubscriber s, string listName) => new()
            {
                Id = s.Id,
                MailingListId = s.MailingListId,
                MailingListName = listName,
                Email = s.Email,
                FirstName = s.FirstName,
                LastName = s.LastName,
                Phone = s.Phone,
                Status = s.Status,
                StatusLabel = s.Status.ToString(),
                Source = s.Source,
                CustomerId = s.CustomerId,
                UnsubscribedAt = s.UnsubscribedAt,
                CreatedAt = s.CreatedAt
            };

        private static CampaignResponseDto MapCampaignToDto(
            Campaign c, List<string> listNames)
        {
            var openRate = c.SentCount > 0
                ? Math.Round((double)c.OpenedCount / c.SentCount * 100, 1) : 0;
            var clickRate = c.SentCount > 0
                ? Math.Round((double)c.ClickedCount / c.SentCount * 100, 1) : 0;

            return new CampaignResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                Subject = c.Subject,
                PreviewText = c.PreviewText,
                FromName = c.FromName,
                FromEmail = c.FromEmail,
                HtmlBody = c.HtmlBody,
                Status = c.Status,
                StatusLabel = c.Status.ToString(),
                ScheduledAt = c.ScheduledAt,
                SentAt = c.SentAt,
                MailingListNames = listNames,
                TotalRecipients = c.TotalRecipients,
                SentCount = c.SentCount,
                OpenedCount = c.OpenedCount,
                ClickedCount = c.ClickedCount,
                BouncedCount = c.BouncedCount,
                UnsubscribedCount = c.UnsubscribedCount,
                OpenRate = openRate,
                ClickRate = clickRate,
                CreatedAt = c.CreatedAt
            };
        }
    }
}