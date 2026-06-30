using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface ITransactionTypeService
    {
        Task<(bool ok, string? error, TransactionTypeResponseDto? data)> CreateAsync(CreateTransactionTypeDto dto);
        Task<List<TransactionTypeResponseDto>> GetAllAsync();
        Task<TransactionTypeResponseDto?> GetByIdAsync(Guid id);
        Task<(bool ok, string? error, TransactionTypeResponseDto? data)> UpdateAsync(Guid id, UpdateTransactionTypeDto dto);
        Task<(bool ok, string? error)> DeleteAsync(Guid id);

        // Transactions scoped to a given type
        Task<(bool ok, string? error, PagedResultDto<TransactionResponseDto>? data)> GetTransactionsAsync(Guid typeId, PaginationQuery query);
        Task<(bool ok, string? error, TransactionResponseDto? data)> UpdateTransactionAsync(Guid typeId, Guid transactionId, UpdateTransactionDto dto);
        Task<(bool ok, string? error)> DeleteTransactionAsync(Guid typeId, Guid transactionId);
    }

    public class TransactionTypeService : ITransactionTypeService
    {
        private readonly AppDbContext _db;

        public TransactionTypeService(AppDbContext db) => _db = db;

        private static TransactionTypeResponseDto Map(TransactionType tt, int count) => new()
        {
            Id = tt.Id,
            Name = tt.Name,
            TransactionsCount = count,
            CreatedAtUtc = tt.CreatedAtUtc
        };

        private static TransactionResponseDto MapTransaction(Transaction t) => new()
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

        public async Task<(bool ok, string? error, TransactionTypeResponseDto? data)> CreateAsync(CreateTransactionTypeDto dto)
        {
            var name = dto.Name.Trim();

            var exists = await _db.TransactionTypes.AnyAsync(tt => tt.Name.ToLower() == name.ToLower());
            if (exists) return (false, "A transaction type with this name already exists.", null);

            var type = new TransactionType { Name = name };
            _db.TransactionTypes.Add(type);
            await _db.SaveChangesAsync();

            return (true, null, Map(type, 0));
        }

        public async Task<List<TransactionTypeResponseDto>> GetAllAsync()
        {
            return await _db.TransactionTypes
                .OrderByDescending(tt => tt.CreatedAtUtc)
                .Select(tt => new TransactionTypeResponseDto
                {
                    Id = tt.Id,
                    Name = tt.Name,
                    TransactionsCount = tt.Transactions.Count,
                    CreatedAtUtc = tt.CreatedAtUtc
                })
                .ToListAsync();
        }

        public async Task<TransactionTypeResponseDto?> GetByIdAsync(Guid id)
        {
            var type = await _db.TransactionTypes
                .Where(tt => tt.Id == id)
                .Select(tt => new TransactionTypeResponseDto
                {
                    Id = tt.Id,
                    Name = tt.Name,
                    TransactionsCount = tt.Transactions.Count,
                    CreatedAtUtc = tt.CreatedAtUtc
                })
                .FirstOrDefaultAsync();

            return type;
        }

        public async Task<(bool ok, string? error, TransactionTypeResponseDto? data)> UpdateAsync(Guid id, UpdateTransactionTypeDto dto)
        {
            var type = await _db.TransactionTypes.FindAsync(id);
            if (type is null) return (false, "Transaction type not found.", null);

            var name = dto.Name.Trim();
            var exists = await _db.TransactionTypes.AnyAsync(tt => tt.Id != id && tt.Name.ToLower() == name.ToLower());
            if (exists) return (false, "A transaction type with this name already exists.", null);

            type.Name = name;
            await _db.SaveChangesAsync();

            var count = await _db.Transactions.CountAsync(t => t.TypeId == id);
            return (true, null, Map(type, count));
        }

        public async Task<(bool ok, string? error)> DeleteAsync(Guid id)
        {
            var type = await _db.TransactionTypes.FindAsync(id);
            if (type is null) return (false, "Transaction type not found.");

            var hasTransactions = await _db.Transactions.AnyAsync(t => t.TypeId == id);
            if (hasTransactions)
                return (false, "Cannot delete a type that still has transactions attached to it. Delete or reassign its transactions first.");

            _db.TransactionTypes.Remove(type);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool ok, string? error, PagedResultDto<TransactionResponseDto>? data)> GetTransactionsAsync(Guid typeId, PaginationQuery query)
        {
            var typeExists = await _db.TransactionTypes.AnyAsync(tt => tt.Id == typeId);
            if (!typeExists) return (false, "Transaction type not found.", null);

            var baseQuery = _db.Transactions
                .Include(t => t.Supervisor)
                .Include(t => t.Type)
                .Where(t => t.TypeId == typeId)
                .OrderByDescending(t => t.CreatedAtUtc);

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(t => MapTransaction(t))
                .ToListAsync();

            var result = new PagedResultDto<TransactionResponseDto>
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize)
            };

            return (true, null, result);
        }

        public async Task<(bool ok, string? error, TransactionResponseDto? data)> UpdateTransactionAsync(Guid typeId, Guid transactionId, UpdateTransactionDto dto)
        {
            var t = await _db.Transactions
                .Include(x => x.Supervisor)
                .Include(x => x.Type)
                .FirstOrDefaultAsync(x => x.Id == transactionId && x.TypeId == typeId);

            if (t is null) return (false, "Transaction not found under this type.", null);

            if (dto.TypeId.HasValue)
            {
                var newTypeExists = await _db.TransactionTypes.AnyAsync(tt => tt.Id == dto.TypeId.Value);
                if (!newTypeExists) return (false, "Transaction type not found.", null);
                t.TypeId = dto.TypeId.Value;
            }

            if (dto.Description is not null) t.Description = dto.Description.Trim();
            if (dto.Price.HasValue) t.Price = dto.Price.Value;
            if (dto.PhoneNumber is not null) t.PhoneNumber = dto.PhoneNumber.Trim();
            t.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _db.Entry(t).Reference(x => x.Type).LoadAsync();
            return (true, null, MapTransaction(t));
        }

        public async Task<(bool ok, string? error)> DeleteTransactionAsync(Guid typeId, Guid transactionId)
        {
            var t = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == transactionId && x.TypeId == typeId);
            if (t is null) return (false, "Transaction not found under this type.");

            _db.Transactions.Remove(t);
            await _db.SaveChangesAsync();
            return (true, null);
        }
    }
}
