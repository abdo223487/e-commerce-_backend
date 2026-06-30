using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface IOrderService
    {
        Task<(bool ok, string? error, OrderResponseDto? data)> CreateOrderAsync(Guid userId, CreateOrderDto dto);
        Task<List<OrderResponseDto>> GetOrdersForUserAsync(Guid userId);
        Task<List<AdminOrderResponseDto>> GetAllOrdersAsync();
        Task<OrderResponseDto?> GetOrderByIdAsync(Guid orderId, Guid? restrictToUserId = null);
        Task<AdminOrderResponseDto?> GetOrderByIdAdminAsync(Guid orderId);
        Task<(bool ok, string? error, AdminOrderResponseDto? data)> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, Guid actorId, string? note);
        Task<(bool ok, string? error)> DeleteOrderAsync(Guid orderId, Guid? restrictToUserId, bool isAdmin, Guid actorId);
    }

    public class OrderService : IOrderService
    {
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;

        // Allowed forward transitions. Anything not listed here is rejected,
        // so the order timeline always tells a coherent, linear story that
        // both the user and the admin/supervisor can trust.
        private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
        {
            [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
            [OrderStatus.Confirmed] = new[] { OrderStatus.Preparing, OrderStatus.Cancelled },
            [OrderStatus.Preparing] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
            [OrderStatus.Shipped] = new[] { OrderStatus.Delivered, OrderStatus.Cancelled },
            [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
            [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
        };

        public OrderService(AppDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private static OrderTimelineEntryDto MapTimeline(OrderStatusHistory h) => new()
        {
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            Note = h.Note,
            CreatedAtUtc = h.CreatedAtUtc
        };

        private static AdminOrderTimelineEntryDto MapTimelineAdmin(OrderStatusHistory h) => new()
        {
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            Note = h.Note,
            CreatedAtUtc = h.CreatedAtUtc,
            ChangedByUserId = h.ChangedByUserId,
            ChangedByName = h.ChangedByUser?.FullName,
            ChangedByRole = h.ChangedByUser?.Role.ToString()
        };

        private static OrderResponseDto MapUser(Order o) => new()
        {
            Id = o.Id,
            Status = o.Status,
            DeliveryAddress = o.DeliveryAddress,
            TotalPrice = o.TotalPrice,
            CreatedAtUtc = o.CreatedAtUtc,
            UpdatedAtUtc = o.UpdatedAtUtc,
            Items = o.Items.Select(i => new OrderItemResponseDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductNameSnapshot,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPriceSnapshot,
                CoinsPerUnit = i.CoinsPerUnitSnapshot
            }).ToList(),
            Timeline = o.StatusHistory
                .OrderBy(h => h.CreatedAtUtc)
                .Select(MapTimeline)
                .ToList()
        };

        private static AdminOrderResponseDto MapAdmin(Order o) => new()
        {
            Id = o.Id,
            UserId = o.UserId,
            UserName = o.User?.FullName,
            UserPhone = o.User?.PhoneNumber,
            Status = o.Status,
            DeliveryAddress = o.DeliveryAddress,
            TotalPrice = o.TotalPrice,
            CreatedAtUtc = o.CreatedAtUtc,
            UpdatedAtUtc = o.UpdatedAtUtc,
            Items = o.Items.Select(i => new OrderItemResponseDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductNameSnapshot,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPriceSnapshot,
                CoinsPerUnit = i.CoinsPerUnitSnapshot
            }).ToList(),
            Timeline = o.StatusHistory
                .OrderBy(h => h.CreatedAtUtc)
                .Select(MapTimelineAdmin)
                .ToList()
        };

        /// <summary>
        /// Atomically decrements stock for one product at the database row
        /// level: UPDATE ... SET stock = stock - qty WHERE id = X AND stock
        /// >= qty. Returns true only if a row was actually affected, i.e.
        /// there really was enough stock. Two concurrent requests racing to
        /// buy the last unit will be serialized by Postgres' row lock on
        /// the UPDATE — only one of them can see stock >= qty true and win;
        /// the other gets 0 rows affected and is told to fail cleanly. This
        /// is what makes order creation safe under concurrency without
        /// needing pessimistic locks or a SERIALIZABLE transaction.
        /// </summary>
        private async Task<bool> TryDecrementStockAsync(Guid productId, int quantity)
        {
            var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Products""
                SET ""StockQuantity"" = ""StockQuantity"" - {quantity}
                WHERE ""Id"" = {productId} AND ""StockQuantity"" >= {quantity}");

            return affected == 1;
        }

        /// <summary>
        /// Atomically restores stock for one product (used on cancellation
        /// or deletion of a still-pending order). No conditional WHERE
        /// needed since adding stock back can never go "negative".
        /// </summary>
        private async Task RestoreStockAsync(Guid productId, int quantity)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Products""
                SET ""StockQuantity"" = ""StockQuantity"" + {quantity}
                WHERE ""Id"" = {productId}");
        }

        public async Task<(bool ok, string? error, OrderResponseDto? data)> CreateOrderAsync(Guid userId, CreateOrderDto dto)
        {
            if (dto.Items.Count == 0)
                return (false, "Order must contain at least one product.", null);

            if (string.IsNullOrWhiteSpace(dto.DeliveryAddress))
                return (false, "Delivery address is required.", null);

            // Merge duplicate product ids in the same request and sort by
            // product id. Sorting guarantees that no matter what order the
            // user listed items in, every concurrent transaction touching
            // the same set of products acquires row locks in the *same*
            // order, which avoids deadlocks between two simultaneous
            // multi-item orders.
            var merged = dto.Items
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderBy(m => m.ProductId)
                .ToList();

            if (merged.Any(m => m.Quantity <= 0))
                return (false, "Quantity must be at least 1 for every item.", null);

            var productIds = merged.Select(m => m.ProductId).ToList();
            var products = await _db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            if (products.Count != productIds.Count)
                return (false, "One or more products were not found.", null);

            var productsById = products.ToDictionary(p => p.Id);

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Step 1: atomically reserve stock for every item, in product-id
            // order. The first item that can't be reserved aborts the whole
            // order — nothing is partially deducted, since everything lives
            // inside one DB transaction.
            var decremented = new List<(Guid ProductId, int Quantity)>();
            foreach (var item in merged)
            {
                var reserved = await TryDecrementStockAsync(item.ProductId, item.Quantity);
                if (!reserved)
                {
                    await transaction.RollbackAsync();
                    var name = productsById[item.ProductId].Name;
                    var available = await _db.Products.AsNoTracking()
                        .Where(p => p.Id == item.ProductId)
                        .Select(p => p.StockQuantity)
                        .FirstOrDefaultAsync();
                    return (false,
                        $"Insufficient stock for '{name}'. Requested: {item.Quantity}, available: {Math.Max(available, 0)}.",
                        null);
                }
                decremented.Add((item.ProductId, item.Quantity));
            }

            // Step 2: build the order using the price/name/coins snapshot
            // we already had (these don't need to be re-read inside the
            // transaction — they're not part of the race condition, only
            // StockQuantity is).
            var order = new Order
            {
                UserId = userId,
                Status = OrderStatus.Pending,
                DeliveryAddress = dto.DeliveryAddress.Trim()
            };

            decimal total = 0;
            foreach (var item in merged)
            {
                var product = productsById[item.ProductId];
                total += product.Price * item.Quantity;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    CategoryIdSnapshot = product.CategoryId,
                    ProductNameSnapshot = product.Name,
                    UnitPriceSnapshot = product.Price,
                    CoinsPerUnitSnapshot = product.CoinsPerUnit,
                    Quantity = item.Quantity
                });
            }

            order.TotalPrice = total;

            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = OrderStatus.Pending,
                ToStatus = OrderStatus.Pending,
                ChangedByUserId = null,
                Note = "Order placed; stock reserved.",
                CreatedAtUtc = DateTime.UtcNow
            });

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await _audit.LogAsync(userId, AuditAction.OrderCreated,
                $"Order created with {order.Items.Count} item(s), total: {total:F2} EGP. Stock reserved: " +
                string.Join(", ", decremented.Select(d => $"{productsById[d.ProductId].Name} x{d.Quantity}")),
                order.Id, "Order", after: new { order.Id, order.TotalPrice, order.Status });

            return (true, null, MapUser(order));
        }

        public async Task<List<OrderResponseDto>> GetOrdersForUserAsync(Guid userId)
        {
            var orders = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync();

            return orders.Select(MapUser).ToList();
        }

        public async Task<List<AdminOrderResponseDto>> GetAllOrdersAsync()
        {
            var orders = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.User)
                .Include(o => o.StatusHistory).ThenInclude(h => h.ChangedByUser)
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync();

            return orders.Select(MapAdmin).ToList();
        }

        public async Task<OrderResponseDto?> GetOrderByIdAsync(Guid orderId, Guid? restrictToUserId = null)
        {
            var query = _db.Orders
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .Where(o => o.Id == orderId);
            if (restrictToUserId.HasValue) query = query.Where(o => o.UserId == restrictToUserId.Value);

            var order = await query.FirstOrDefaultAsync();
            return order is null ? null : MapUser(order);
        }

        public async Task<AdminOrderResponseDto?> GetOrderByIdAdminAsync(Guid orderId)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.User)
                .Include(o => o.StatusHistory).ThenInclude(h => h.ChangedByUser)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            return order is null ? null : MapAdmin(order);
        }

        public async Task<(bool ok, string? error, AdminOrderResponseDto? data)> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, Guid actorId, string? note)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var order = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.User)
                .Include(o => o.StatusHistory).ThenInclude(h => h.ChangedByUser)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null) return (false, "Order not found.", null);

            if (order.Status == newStatus)
                return (false, $"Order is already {newStatus}.", null);

            var allowed = AllowedTransitions.TryGetValue(order.Status, out var next) && next.Contains(newStatus);
            if (!allowed)
                return (false, $"Cannot move order from {order.Status} to {newStatus}.", null);

            var oldStatus = order.Status;
            order.Status = newStatus;
            order.UpdatedAtUtc = DateTime.UtcNow;

            // Cancelling at any point before delivery releases the
            // reserved stock back atomically, same mechanism as creation.
            if (newStatus == OrderStatus.Cancelled)
            {
                foreach (var item in order.Items.Where(i => i.ProductId.HasValue))
                    await RestoreStockAsync(item.ProductId!.Value, item.Quantity);
            }

            if (newStatus == OrderStatus.Delivered && !order.CoinsAwarded && order.User is not null)
            {
                var coinsEarned = order.Items.Sum(i => i.CoinsPerUnitSnapshot * i.Quantity);
                order.User.Coins += coinsEarned;
                order.CoinsAwarded = true;
            }

            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = oldStatus,
                ToStatus = newStatus,
                ChangedByUserId = actorId,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await _audit.LogAsync(actorId, AuditAction.OrderStatusChanged,
                $"Order {orderId} status changed from {oldStatus} to {newStatus}" +
                (string.IsNullOrWhiteSpace(note) ? "" : $" — note: {note}"),
                orderId, "Order",
                before: new { Status = oldStatus.ToString() },
                after: new { Status = newStatus.ToString() });

            // Reload changed-by user for the freshly added history row so the mapper has a name.
            await _db.Entry(order.StatusHistory.Last()).Reference(h => h.ChangedByUser).LoadAsync();

            return (true, null, MapAdmin(order));
        }

        public async Task<(bool ok, string? error)> DeleteOrderAsync(Guid orderId, Guid? restrictToUserId, bool isAdmin, Guid actorId)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var query = _db.Orders.Include(o => o.User).Include(o => o.Items).Where(o => o.Id == orderId);
            if (restrictToUserId.HasValue) query = query.Where(o => o.UserId == restrictToUserId.Value);

            var order = await query.FirstOrDefaultAsync();
            if (order is null) return (false, "Order not found.");

            if (!isAdmin && order.Status != OrderStatus.Pending)
                return (false, "Only pending orders can be deleted by the user. Contact support otherwise.");

            // If the order still has stock reserved against it (i.e. it
            // never reached Cancelled, which already released stock), give
            // that stock back before removing the order so deleting an
            // order never silently "loses" inventory.
            if (order.Status != OrderStatus.Cancelled)
            {
                foreach (var item in order.Items.Where(i => i.ProductId.HasValue))
                    await RestoreStockAsync(item.ProductId!.Value, item.Quantity);
            }

            var snapshot = new { order.Id, order.Status, order.TotalPrice, UserName = order.User?.FullName };
            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await _audit.LogAsync(actorId, AuditAction.OrderDeleted,
                $"Order {orderId} deleted (was {order.Status}, total: {order.TotalPrice:F2} EGP)",
                orderId, "Order", before: snapshot);

            return (true, null);
        }
    }
}
