using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Reviews;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReviewService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ════════════════════════════════════════════════════════════════
        // SUBMIT
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<ReviewResponseDto>> SubmitAsync(CreateReviewDto dto)
        {
            // ── Validate rating ──────────────────────────────────────────────
            if (dto.Rating < 1 || dto.Rating > 5)
                return ApiResponse<ReviewResponseDto>.Fail("Rating must be between 1 and 5.");

            // ── Validate reviewer identity ───────────────────────────────────
            if (dto.CustomerId == null &&
                string.IsNullOrWhiteSpace(dto.ReviewerName))
                return ApiResponse<ReviewResponseDto>.Fail(
                    "Reviewer name is required for guest reviews.");

            // ── Product exists? ──────────────────────────────────────────────
            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
            if (product == null || product.TenantId != dto.TenantId)
                return ApiResponse<ReviewResponseDto>.Fail("Product not found.");

            // ── Duplicate check ──────────────────────────────────────────────
            if (dto.CustomerId.HasValue)
            {
                var duplicate = await _unitOfWork.ProductReviews.FindAsync(r =>
                    r.ProductId == dto.ProductId &&
                    r.CustomerId == dto.CustomerId &&
                    r.TenantId == dto.TenantId);

                if (duplicate.Any())
                    return ApiResponse<ReviewResponseDto>.Fail(
                        "You have already reviewed this product.");
            }
            else if (!string.IsNullOrWhiteSpace(dto.ReviewerEmail))
            {
                var duplicate = await _unitOfWork.ProductReviews.FindAsync(r =>
                    r.ProductId == dto.ProductId &&
                    r.ReviewerEmail == dto.ReviewerEmail.ToLower().Trim() &&
                    r.TenantId == dto.TenantId);

                if (duplicate.Any())
                    return ApiResponse<ReviewResponseDto>.Fail(
                        "A review from this email already exists for this product.");
            }

            // ── Reviewer name from Customer entity ───────────────────────────
            string reviewerName = dto.ReviewerName.Trim();
            string reviewerEmail = dto.ReviewerEmail.ToLower().Trim();

            if (dto.CustomerId.HasValue)
            {
                var customer = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId.Value);
                if (customer != null)
                {
                    reviewerName = $"{customer.FirstName} {customer.LastName}".Trim();
                    reviewerEmail = customer.Email;
                }
            }

            // ── Verified Purchase check ──────────────────────────────────────
            bool isVerified = false;
            if (dto.CustomerId.HasValue)
            {
                var orders = await _unitOfWork.Orders.FindAsync(o =>
                    o.TenantId == dto.TenantId &&
                    o.CustomerId == dto.CustomerId &&
                    (o.Status == OrderStatus.Delivered ||
                     o.Status == OrderStatus.Completed));

                foreach (var order in orders)
                {
                    var hasProduct = order.Items?.Any(i => i.ProductId == dto.ProductId) ?? false;
                    if (hasProduct) { isVerified = true; break; }
                }
            }

            // ── Auto-approve setting (key: reviews_auto_approve) ─────────────
            var settings = await _unitOfWork.Settings.FindAsync(s =>
                s.TenantId == dto.TenantId &&
                s.Key == "reviews_auto_approve");

            bool autoApprove = settings.FirstOrDefault()?.Value == "true";
            var status = autoApprove ? ReviewStatus.Approved : ReviewStatus.Pending;

            var review = new ProductReview
            {
                TenantId = dto.TenantId,
                ProductId = dto.ProductId,
                CustomerId = dto.CustomerId,
                ReviewerName = reviewerName,
                ReviewerEmail = reviewerEmail,
                Rating = dto.Rating,
                Title = dto.Title.Trim(),
                Body = dto.Body.Trim(),
                Status = status,
                IsVerifiedPurchase = isVerified
            };

            await _unitOfWork.ProductReviews.AddAsync(review);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ReviewResponseDto>.Ok(
                MapToDto(review, product.Name),
                "Review submitted successfully.");
        }

        // ════════════════════════════════════════════════════════════════
        // HELPFUL VOTE
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<bool>> MarkHelpfulAsync(Guid reviewId)
        {
            var review = await _unitOfWork.ProductReviews.GetByIdAsync(reviewId);
            if (review == null)
                return ApiResponse<bool>.Fail("Review not found.");

            if (review.Status != ReviewStatus.Approved)
                return ApiResponse<bool>.Fail("Can only vote on approved reviews.");

            review.HelpfulCount++;
            await _unitOfWork.ProductReviews.UpdateAsync(review);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true);
        }

        // ════════════════════════════════════════════════════════════════
        // GET — TENANT
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<PagedResponse<ReviewResponseDto>>> GetAllByTenantAsync(
            Guid tenantId,
            ReviewStatus? status,
            PaginationParams pagination)
        {
            var (items, total) = await _unitOfWork.ProductReviews.GetPagedAsync(
                r => r.TenantId == tenantId &&
                     (!status.HasValue || r.Status == status.Value),
                pagination.Skip,
                pagination.PageSize);

            // جيب أسماء المنتجات دفعة واحدة
            var productIds = items.Select(r => r.ProductId).Distinct().ToHashSet();
            var products = (await _unitOfWork.Products.FindAsync(p =>
                                  productIds.Contains(p.Id)))
                             .ToDictionary(p => p.Id, p => p.Name);

            var dtos = items
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => MapToDto(r, products.GetValueOrDefault(r.ProductId, string.Empty)))
                .ToList();

            return ApiResponse<PagedResponse<ReviewResponseDto>>.Ok(
                PagedResponse<ReviewResponseDto>.Create(dtos, total, pagination));
        }

        public async Task<ApiResponse<PagedResponse<ReviewResponseDto>>> GetByProductAsync(
            Guid productId,
            ReviewStatus? status,
            PaginationParams pagination)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            var productName = product?.Name ?? string.Empty;

            var (items, total) = await _unitOfWork.ProductReviews.GetPagedAsync(
                r => r.ProductId == productId &&
                     (!status.HasValue || r.Status == status.Value),
                pagination.Skip,
                pagination.PageSize);

            var dtos = items
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => MapToDto(r, productName))
                .ToList();

            return ApiResponse<PagedResponse<ReviewResponseDto>>.Ok(
                PagedResponse<ReviewResponseDto>.Create(dtos, total, pagination));
        }

        public async Task<ApiResponse<ReviewResponseDto>> GetByIdAsync(Guid id)
        {
            var review = await _unitOfWork.ProductReviews.GetByIdAsync(id);
            if (review == null)
                return ApiResponse<ReviewResponseDto>.Fail("Review not found.");

            var product = await _unitOfWork.Products.GetByIdAsync(review.ProductId);
            return ApiResponse<ReviewResponseDto>.Ok(
                MapToDto(review, product?.Name ?? string.Empty));
        }

        // ════════════════════════════════════════════════════════════════
        // MODERATION
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<ReviewResponseDto>> UpdateStatusAsync(
            Guid id, UpdateReviewStatusDto dto)
        {
            var review = await _unitOfWork.ProductReviews.GetByIdAsync(id);
            if (review == null)
                return ApiResponse<ReviewResponseDto>.Fail("Review not found.");

            review.Status = dto.Status;
            await _unitOfWork.ProductReviews.UpdateAsync(review);
            await _unitOfWork.SaveChangesAsync();

            var product = await _unitOfWork.Products.GetByIdAsync(review.ProductId);
            return ApiResponse<ReviewResponseDto>.Ok(
                MapToDto(review, product?.Name ?? string.Empty),
                $"Review status updated to {dto.Status}.");
        }

        public async Task<ApiResponse<ReviewResponseDto>> AddOwnerReplyAsync(
            Guid id, OwnerReplyDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Reply))
                return ApiResponse<ReviewResponseDto>.Fail("Reply cannot be empty.");

            var review = await _unitOfWork.ProductReviews.GetByIdAsync(id);
            if (review == null)
                return ApiResponse<ReviewResponseDto>.Fail("Review not found.");

            review.OwnerReply = dto.Reply.Trim();
            review.OwnerRepliedAt = DateTime.UtcNow;

            await _unitOfWork.ProductReviews.UpdateAsync(review);
            await _unitOfWork.SaveChangesAsync();

            var product = await _unitOfWork.Products.GetByIdAsync(review.ProductId);
            return ApiResponse<ReviewResponseDto>.Ok(
                MapToDto(review, product?.Name ?? string.Empty),
                "Reply added successfully.");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var review = await _unitOfWork.ProductReviews.GetByIdAsync(id);
            if (review == null)
                return ApiResponse<bool>.Fail("Review not found.");

            await _unitOfWork.ProductReviews.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Review deleted successfully.");
        }

        // ════════════════════════════════════════════════════════════════
        // PUBLIC SUMMARY
        // ════════════════════════════════════════════════════════════════

        public async Task<ApiResponse<ProductRatingSummaryDto>> GetProductSummaryAsync(
            Guid productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return ApiResponse<ProductRatingSummaryDto>.Fail("Product not found.");

            // جيب كل المراجعات المعتمدة
            var approved = (await _unitOfWork.ProductReviews.FindAsync(r =>
                r.ProductId == productId &&
                r.Status == ReviewStatus.Approved))
                .ToList();

            // Breakdown 1..5
            var breakdown = Enumerable.Range(1, 5)
                .ToDictionary(
                    star => star,
                    star => approved.Count(r => r.Rating == star));

            double avg = approved.Any()
                ? Math.Round(approved.Average(r => r.Rating), 1)
                : 0;

            // أحدث 5 مراجعات معتمدة
            var recent = approved
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => MapToDto(r, product.Name))
                .ToList();

            var summary = new ProductRatingSummaryDto
            {
                ProductId = productId,
                ProductName = product.Name,
                AverageRating = avg,
                TotalReviews = approved.Count,
                RatingBreakdown = breakdown,
                RecentReviews = recent
            };

            return ApiResponse<ProductRatingSummaryDto>.Ok(summary);
        }

        // ════════════════════════════════════════════════════════════════
        // MAPPER
        // ════════════════════════════════════════════════════════════════

        private static ReviewResponseDto MapToDto(ProductReview r, string productName) => new()
        {
            Id = r.Id,
            ProductId = r.ProductId,
            ProductName = productName,
            CustomerId = r.CustomerId,
            ReviewerName = r.ReviewerName,
            ReviewerEmail = r.ReviewerEmail,
            Rating = r.Rating,
            Title = r.Title,
            Body = r.Body,
            Status = r.Status,
            StatusLabel = r.Status.ToString(),
            IsVerifiedPurchase = r.IsVerifiedPurchase,
            OwnerReply = r.OwnerReply,
            OwnerRepliedAt = r.OwnerRepliedAt,
            HelpfulCount = r.HelpfulCount,
            CreatedAt = r.CreatedAt
        };
    }
}