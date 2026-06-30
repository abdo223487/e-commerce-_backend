using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface ICustomOrderService
    {
        Task<(bool ok, string? error, CustomOrderResponseDto? data)> CreateAsync(Guid userId, string userName, CreateCustomOrderDto dto);
        Task<List<CustomOrderResponseDto>> GetForUserAsync(Guid userId);
        Task<List<AdminCustomOrderResponseDto>> GetAllAsync();
        Task<CustomOrderResponseDto?> GetByIdAsync(Guid id, Guid? restrictToUserId, bool isAdmin);
        Task<(bool ok, string? error, CustomOrderMessageResponseDto? data)> AddMessageAsync(
            Guid customOrderId, Guid senderId, string senderName, bool senderIsAdmin, CreateCustomOrderMessageDto dto, Guid? restrictToUserId);
        Task<(bool ok, string? error, AdminCustomOrderResponseDto? data)> UpdateStatusAsync(Guid id, CustomOrderStatus status);
        Task<(bool ok, string? error)> DeleteAsync(Guid id);
    }

    public class CustomOrderService : ICustomOrderService
    {
        private readonly AppDbContext _db;

        public CustomOrderService(AppDbContext db)
        {
            _db = db;
        }

        private static List<CustomOrderMessageResponseDto> MapMessages(CustomOrder co) =>
            co.Messages
                .OrderBy(m => m.CreatedAtUtc)
                .Select(m => new CustomOrderMessageResponseDto
                {
                    Id = m.Id,
                    SenderName = m.SenderName,
                    SenderIsAdmin = m.SenderIsAdmin,
                    Content = m.Content,
                    CreatedAtUtc = m.CreatedAtUtc
                }).ToList();

        private static CustomOrderResponseDto MapUser(CustomOrder co) => new()
        {
            Id = co.Id,
            Title = co.Title,
            Status = co.Status,
            CreatedAtUtc = co.CreatedAtUtc,
            UpdatedAtUtc = co.UpdatedAtUtc,
            Messages = MapMessages(co)
        };

        private static AdminCustomOrderResponseDto MapAdmin(CustomOrder co) => new()
        {
            Id = co.Id,
            UserId = co.UserId,
            UserName = co.User?.FullName,
            Title = co.Title,
            Status = co.Status,
            CreatedAtUtc = co.CreatedAtUtc,
            UpdatedAtUtc = co.UpdatedAtUtc,
            Messages = MapMessages(co)
        };

        public async Task<(bool ok, string? error, CustomOrderResponseDto? data)> CreateAsync(Guid userId, string userName, CreateCustomOrderDto dto)
        {
            var customOrder = new CustomOrder
            {
                UserId = userId,
                Title = dto.Title.Trim(),
                Status = CustomOrderStatus.Open
            };

            if (!string.IsNullOrWhiteSpace(dto.InitialMessage))
            {
                customOrder.Messages.Add(new CustomOrderMessage
                {
                    SenderId = userId,
                    SenderName = userName,
                    SenderIsAdmin = false,
                    Content = dto.InitialMessage.Trim()
                });
            }

            _db.CustomOrders.Add(customOrder);
            await _db.SaveChangesAsync();

            return (true, null, MapUser(customOrder));
        }

        public async Task<List<CustomOrderResponseDto>> GetForUserAsync(Guid userId)
        {
            var orders = await _db.CustomOrders
                .Include(co => co.Messages)
                .Where(co => co.UserId == userId)
                .OrderByDescending(co => co.UpdatedAtUtc ?? co.CreatedAtUtc)
                .ToListAsync();

            return orders.Select(MapUser).ToList();
        }

        public async Task<List<AdminCustomOrderResponseDto>> GetAllAsync()
        {
            var orders = await _db.CustomOrders
                .Include(co => co.Messages)
                .Include(co => co.User)
                .OrderByDescending(co => co.UpdatedAtUtc ?? co.CreatedAtUtc)
                .ToListAsync();

            return orders.Select(MapAdmin).ToList();
        }

        public async Task<CustomOrderResponseDto?> GetByIdAsync(Guid id, Guid? restrictToUserId, bool isAdmin)
        {
            var query = _db.CustomOrders.Include(co => co.Messages).Include(co => co.User).Where(co => co.Id == id);
            if (restrictToUserId.HasValue) query = query.Where(co => co.UserId == restrictToUserId.Value);

            var order = await query.FirstOrDefaultAsync();
            if (order is null) return null;
            return isAdmin ? MapAdmin(order) : MapUser(order);
        }

        public async Task<(bool ok, string? error, CustomOrderMessageResponseDto? data)> AddMessageAsync(
            Guid customOrderId, Guid senderId, string senderName, bool senderIsAdmin, CreateCustomOrderMessageDto dto, Guid? restrictToUserId)
        {
            var query = _db.CustomOrders.Where(co => co.Id == customOrderId);
            if (restrictToUserId.HasValue) query = query.Where(co => co.UserId == restrictToUserId.Value);

            var customOrder = await query.FirstOrDefaultAsync();
            if (customOrder is null) return (false, "Custom order not found.", null);

            if (customOrder.Status == CustomOrderStatus.Closed)
                return (false, "This custom order is closed; no further messages can be added.", null);

            var message = new CustomOrderMessage
            {
                CustomOrderId = customOrderId,
                SenderId = senderId,
                SenderName = senderName,
                SenderIsAdmin = senderIsAdmin,
                Content = dto.Content.Trim()
            };

            _db.CustomOrderMessages.Add(message);

            if (senderIsAdmin && customOrder.Status == CustomOrderStatus.Open)
                customOrder.Status = CustomOrderStatus.InProgress;

            customOrder.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return (true, null, new CustomOrderMessageResponseDto
            {
                Id = message.Id,
                SenderName = message.SenderName,
                SenderIsAdmin = message.SenderIsAdmin,
                Content = message.Content,
                CreatedAtUtc = message.CreatedAtUtc
            });
        }

        public async Task<(bool ok, string? error, AdminCustomOrderResponseDto? data)> UpdateStatusAsync(Guid id, CustomOrderStatus status)
        {
            var order = await _db.CustomOrders.Include(co => co.Messages).Include(co => co.User)
                .FirstOrDefaultAsync(co => co.Id == id);
            if (order is null) return (false, "Custom order not found.", null);

            order.Status = status;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return (true, null, MapAdmin(order));
        }

        public async Task<(bool ok, string? error)> DeleteAsync(Guid id)
        {
            var order = await _db.CustomOrders.FirstOrDefaultAsync(co => co.Id == id);
            if (order is null) return (false, "Custom order not found.");

            _db.CustomOrders.Remove(order);
            await _db.SaveChangesAsync();
            return (true, null);
        }
    }
}
