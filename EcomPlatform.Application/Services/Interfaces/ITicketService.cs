using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Tickets;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ITicketService
    {
        Task<ApiResponse<TicketResponseDto>> CreateAsync(CreateTicketDto dto);
        Task<ApiResponse<TicketResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<PagedResponse<TicketResponseDto>>> GetAllByTenantAsync(Guid tenantId, PaginationParams pagination);
        Task<ApiResponse<PagedResponse<TicketResponseDto>>> GetAllAsync(PaginationParams pagination);
        Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, TicketStatus status);
        Task<ApiResponse<TicketReplyResponseDto>> AddReplyAsync(CreateTicketReplyDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
    }
}