using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface ITransactionService
    {
        Task<(bool ok, string? error, TransactionResponseDto? data)> CreateAsync(Guid supervisorId, CreateTransactionDto dto);
        Task<List<TransactionResponseDto>> GetLastFourAsync(Guid supervisorId);
        Task<(bool ok, string? error, TransactionResponseDto? data)> UpdateAsync(Guid supervisorId, Guid transactionId, UpdateTransactionDto dto);
        Task<(bool ok, string? error)> DeleteAsync(Guid supervisorId, Guid transactionId);
        Task<List<TransactionResponseDto>> GetAllAsync();
        Task<PagedResultDto<TransactionResponseDto>> GetAllPagedAsync(PaginationQuery query);
    }

    public class TransactionService : ITransactionService
    {
        private readonly AppDbContext _db;

        public TransactionService(AppDbContext db) => _db = db;

        private static TransactionResponseDto Map(Transaction t) => new()
        {
            Id = t.Id,
            SupervisorId = t.SupervisorId,
            SupervisorName = t.Supervisor?.FullName ?? string.Empty,
            TypeId = t.TypeId,
            TypeName = t.Type?.Name ?? string.Empty,
            Description = t.Description,
            Price = t.Price,
            PhoneNumber = t.PhoneNumber,
            CreatedAtUtc = t.CreatedAtUtc,
            UpdatedAtUtc = t.UpdatedAtUtc
        };

        public async Task<(bool ok, string? error, TransactionResponseDto? data)> CreateAsync(Guid supervisorId, CreateTransactionDto dto)
        {
            if (dto.TypeId == Guid.Empty) return (false, "TypeId is required.", null);

            var typeExists = await _db.TransactionTypes.AnyAsync(tt => tt.Id == dto.TypeId);
            if (!typeExists) return (false, "Transaction type not found.", null);

            var t = new Transaction
            {
                SupervisorId = supervisorId,
                TypeId = dto.TypeId,
                Description = dto.Description.Trim(),
                Price = dto.Price,
                PhoneNumber = dto.PhoneNumber.Trim()
            };

            _db.Transactions.Add(t);
            await _db.SaveChangesAsync();

            await _db.Entry(t).Reference(x => x.Supervisor).LoadAsync();
            await _db.Entry(t).Reference(x => x.Type).LoadAsync();
            return (true, null, Map(t));
        }

        public async Task<List<TransactionResponseDto>> GetLastFourAsync(Guid supervisorId)
        {
            return await _db.Transactions
                .Include(t => t.Supervisor)
                .Include(t => t.Type)
                .Where(t => t.SupervisorId == supervisorId)
                .OrderByDescending(t => t.CreatedAtUtc)
                .Take(4)
                .Select(t => Map(t))
                .ToListAsync();
        }

        public async Task<(bool ok, string? error, TransactionResponseDto? data)> UpdateAsync(Guid supervisorId, Guid transactionId, UpdateTransactionDto dto)
        {
            var t = await _db.Transactions
                .Include(x => x.Supervisor)
                .Include(x => x.Type)
                .FirstOrDefaultAsync(x => x.Id == transactionId && x.SupervisorId == supervisorId);

            if (t is null) return (false, "Transaction not found.", null);

            if (dto.TypeId.HasValue)
            {
                var typeExists = await _db.TransactionTypes.AnyAsync(tt => tt.Id == dto.TypeId.Value);
                if (!typeExists) return (false, "Transaction type not found.", null);
                t.TypeId = dto.TypeId.Value;
            }

            if (dto.Description is not null) t.Description = dto.Description.Trim();
            if (dto.Price.HasValue) t.Price = dto.Price.Value;
            if (dto.PhoneNumber is not null) t.PhoneNumber = dto.PhoneNumber.Trim();
            t.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _db.Entry(t).Reference(x => x.Type).LoadAsync();
            return (true, null, Map(t));
        }

        public async Task<(bool ok, string? error)> DeleteAsync(Guid supervisorId, Guid transactionId)
        {
            var t = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == transactionId && x.SupervisorId == supervisorId);
            if (t is null) return (false, "Transaction not found.");

            _db.Transactions.Remove(t);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<List<TransactionResponseDto>> GetAllAsync()
        {
            return await _db.Transactions
                .Include(t => t.Supervisor)
                .Include(t => t.Type)
                .OrderByDescending(t => t.CreatedAtUtc)
                .Select(t => Map(t))
                .ToListAsync();
        }

        public async Task<PagedResultDto<TransactionResponseDto>> GetAllPagedAsync(PaginationQuery query)
        {
            var baseQuery = _db.Transactions
                .Include(t => t.Supervisor)
                .Include(t => t.Type)
                .OrderByDescending(t => t.CreatedAtUtc);

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(t => Map(t))
                .ToListAsync();

            return new PagedResultDto<TransactionResponseDto>
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize)
            };
        }
    }
}
