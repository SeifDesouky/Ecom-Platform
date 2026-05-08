using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Tickets;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class TicketService : ITicketService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public TicketService(IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<ApiResponse<TicketResponseDto>> CreateAsync(CreateTicketDto dto)
        {
            var ticket = new Ticket
            {
                Subject = dto.Subject,
                Message = dto.Message,
                Priority = dto.Priority,
                Category = dto.Category,
                TenantId = dto.TenantId,
                CreatedById = dto.CreatedById,
                Status = TicketStatus.Open
            };

            await _unitOfWork.Tickets.AddAsync(ticket);
            await _unitOfWork.SaveChangesAsync();

            var user = await _unitOfWork.Users.GetByIdAsync(dto.CreatedById);
            ticket.CreatedBy = user;

            return ApiResponse<TicketResponseDto>.Ok(MapToDto(ticket), "Ticket created successfully");
        }

        public async Task<ApiResponse<TicketResponseDto>> GetByIdAsync(Guid id)
        {
            var ticket = await _unitOfWork.Tickets.GetByIdAsync(id);
            if (ticket == null)
                return ApiResponse<TicketResponseDto>.Fail("Ticket not found");

            var replies = await _unitOfWork.TicketReplies.FindAsync(r => r.TicketId == id);
            ticket.Replies = replies.ToList();

            var user = await _unitOfWork.Users.GetByIdAsync(ticket.CreatedById);
            ticket.CreatedBy = user;

            return ApiResponse<TicketResponseDto>.Ok(MapToDto(ticket));
        }

        public async Task<ApiResponse<PagedResponse<TicketResponseDto>>> GetAllByTenantAsync(Guid tenantId, PaginationParams pagination)
        {
            var all = await _unitOfWork.Tickets.FindAsync(t => t.TenantId == tenantId);
            var totalCount = all.Count();
            var items = all
                .OrderByDescending(t => t.CreatedAt)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .Select(MapToDto)
                .ToList();
            var result = PagedResponse<TicketResponseDto>.Create(items, totalCount, pagination);
            return ApiResponse<PagedResponse<TicketResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<PagedResponse<TicketResponseDto>>> GetAllAsync(PaginationParams pagination)
        {
            var all = await _unitOfWork.Tickets.GetAllAsync();
            var totalCount = all.Count();
            var items = all
                .OrderByDescending(t => t.CreatedAt)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .Select(MapToDto)
                .ToList();
            var result = PagedResponse<TicketResponseDto>.Create(items, totalCount, pagination);
            return ApiResponse<PagedResponse<TicketResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, TicketStatus status)
        {
            var ticket = await _unitOfWork.Tickets.GetByIdAsync(id);
            if (ticket == null)
                return ApiResponse<bool>.Fail("Ticket not found");

            ticket.Status = status;
            await _unitOfWork.Tickets.UpdateAsync(ticket);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Ticket status updated successfully");
        }

        public async Task<ApiResponse<TicketReplyResponseDto>> AddReplyAsync(CreateTicketReplyDto dto)
        {
            var ticket = await _unitOfWork.Tickets.GetByIdAsync(dto.TicketId);
            if (ticket == null)
                return ApiResponse<TicketReplyResponseDto>.Fail("Ticket not found");

            var reply = new TicketReply
            {
                Message = dto.Message,
                IsStaff = dto.IsStaff,
                TicketId = dto.TicketId,
                CreatedById = dto.CreatedById
            };

            if (dto.IsStaff)
            {
                ticket.Status = TicketStatus.InProgress;
                await _unitOfWork.Tickets.UpdateAsync(ticket);
            }

            await _unitOfWork.TicketReplies.AddAsync(reply);
            await _unitOfWork.SaveChangesAsync();

            var user = await _unitOfWork.Users.GetByIdAsync(dto.CreatedById);

            // Notify customer when staff replies
            if (dto.IsStaff)
            {
                var ticketCreator = await _unitOfWork.Users.GetByIdAsync(ticket.CreatedById);
                if (ticketCreator != null && !string.IsNullOrEmpty(ticketCreator.Email))
                {
                    _ = _emailService.SendTicketReplyAsync(
                        ticketCreator.Email,
                        $"{ticketCreator.FirstName} {ticketCreator.LastName}".Trim(),
                        ticket.Subject,
                        dto.Message);
                }
            }

            return ApiResponse<TicketReplyResponseDto>.Ok(new TicketReplyResponseDto
            {
                Id = reply.Id,
                Message = reply.Message,
                IsStaff = reply.IsStaff,
                CreatedById = reply.CreatedById,
                CreatedByName = user != null ? $"{user.FirstName} {user.LastName}" : string.Empty,
                CreatedAt = reply.CreatedAt
            }, "Reply added successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var ticket = await _unitOfWork.Tickets.GetByIdAsync(id);
            if (ticket == null)
                return ApiResponse<bool>.Fail("Ticket not found");

            await _unitOfWork.Tickets.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Ticket deleted successfully");
        }

        private static TicketResponseDto MapToDto(Ticket ticket) => new()
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            Message = ticket.Message,
            Status = ticket.Status,
            Priority = ticket.Priority,
            Category = ticket.Category,
            TenantId = ticket.TenantId ?? Guid.Empty,
            CreatedById = ticket.CreatedById,
            CreatedByName = ticket.CreatedBy != null
                ? $"{ticket.CreatedBy.FirstName} {ticket.CreatedBy.LastName}"
                : string.Empty,
            CreatedAt = ticket.CreatedAt,
            Replies = ticket.Replies?.Select(r => new TicketReplyResponseDto
            {
                Id = r.Id,
                Message = r.Message,
                IsStaff = r.IsStaff,
                CreatedById = r.CreatedById,
                CreatedByName = r.CreatedBy != null
                    ? $"{r.CreatedBy.FirstName} {r.CreatedBy.LastName}"
                    : string.Empty,
                CreatedAt = r.CreatedAt
            }).ToList() ?? new()
        };
    }
}