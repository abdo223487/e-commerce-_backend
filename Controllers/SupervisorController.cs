using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Helpers;
using MarketplaceApi.Models;
using MarketplaceApi.Services;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/supervisor")]
    [Authorize(Policy = "SupervisorOnly")]
    [Produces("application/json")]
    public class SupervisorController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SupervisorController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>Supervisor: profile with name and phone.</summary>
        [HttpGet("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Me()
        {
            var userId = User.GetUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound(new { error = "User not found." });

            return Ok(new
            {
                name = user.FullName,
                phone = user.PhoneNumber,
                role = user.Role.ToString()
            });
        }

        /// <summary>Supervisor: stats — count of admins, count of users, total profits (transactions + delivered orders).</summary>
        [HttpGet("stats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Stats()
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin);
            var userCount = await _db.Users.CountAsync(u => u.Role == UserRole.User);
            var supervisorCount = await _db.Users.CountAsync(u => u.Role == UserRole.Supervisor);

            var ordersTotal = await _db.Orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0m;

            var transactionsTotal = await _db.Transactions
                .SumAsync(t => (decimal?)t.Price) ?? 0m;

            return Ok(new
            {
                adminCount,
                userCount,
                supervisorCount,
                ordersRevenue = ordersTotal,
                transactionsRevenue = transactionsTotal,
                totalProfits = ordersTotal + transactionsTotal
            });
        }

        /// <summary>Supervisor: list all regular users ("p" = paginated), ?page=1&amp;pageSize=10.</summary>
        [HttpGet("users")]
        [ProducesResponseType(typeof(PagedResultDto<UserListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers([FromQuery] PaginationQuery query)
        {
            var baseQuery = _db.Users
                .Where(u => u.Role == UserRole.User)
                .OrderByDescending(u => u.CreatedAtUtc);

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role.ToString(),
                    Coins = u.Coins,
                    CreatedAtUtc = u.CreatedAtUtc
                })
                .ToListAsync();

            return Ok(new PagedResultDto<UserListItemDto>
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize)
            });
        }

        /// <summary>Supervisor: list all admins ("p" = paginated), ?page=1&amp;pageSize=10.</summary>
        [HttpGet("admins")]
        [ProducesResponseType(typeof(PagedResultDto<UserListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAdmins([FromQuery] PaginationQuery query)
        {
            var baseQuery = _db.Users
                .Where(u => u.Role == UserRole.Admin)
                .OrderByDescending(u => u.CreatedAtUtc);

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role.ToString(),
                    Coins = u.Coins,
                    CreatedAtUtc = u.CreatedAtUtc
                })
                .ToListAsync();

            return Ok(new PagedResultDto<UserListItemDto>
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize)
            });
        }

        /// <summary>Supervisor: total profits — delivered orders revenue + transactions revenue.</summary>
        [HttpGet("profits")]
        [ProducesResponseType(typeof(ProfitsResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Profits()
        {
            var ordersTotal = await _db.Orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0m;

            var transactionsTotal = await _db.Transactions
                .SumAsync(t => (decimal?)t.Price) ?? 0m;

            return Ok(new ProfitsResponseDto
            {
                OrdersRevenue = ordersTotal,
                TransactionsRevenue = transactionsTotal,
                TotalProfits = ordersTotal + transactionsTotal
            });
        }

        /// <summary>Supervisor: download Excel sheet of all orders (items, user phone, price, total).</summary>
        [HttpGet("export/orders")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportOrders()
        {
            var orders = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Orders");

            // Header row
            var headers = new[] { "Order ID", "Status", "User Name", "User Phone", "Delivery Address", "Item Name", "Qty", "Unit Price", "Line Total", "Order Total", "Created At" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 2;
            decimal grandTotal = 0m;
            foreach (var order in orders)
            {
                if (order.Items.Count == 0)
                {
                    ws.Cell(row, 1).Value = order.Id.ToString();
                    ws.Cell(row, 2).Value = order.Status.ToString();
                    ws.Cell(row, 3).Value = order.User?.FullName ?? "";
                    ws.Cell(row, 4).Value = order.User?.PhoneNumber ?? "";
                    ws.Cell(row, 5).Value = order.DeliveryAddress;
                    ws.Cell(row, 6).Value = "(no items)";
                    ws.Cell(row, 10).Value = order.TotalPrice;
                    ws.Cell(row, 11).Value = order.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm");
                    row++;
                }
                else
                {
                    foreach (var item in order.Items)
                    {
                        ws.Cell(row, 1).Value = order.Id.ToString();
                        ws.Cell(row, 2).Value = order.Status.ToString();
                        ws.Cell(row, 3).Value = order.User?.FullName ?? "";
                        ws.Cell(row, 4).Value = order.User?.PhoneNumber ?? "";
                        ws.Cell(row, 5).Value = order.DeliveryAddress;
                        ws.Cell(row, 6).Value = item.ProductNameSnapshot;
                        ws.Cell(row, 7).Value = item.Quantity;
                        ws.Cell(row, 8).Value = item.UnitPriceSnapshot;
                        ws.Cell(row, 9).Value = item.UnitPriceSnapshot * item.Quantity;
                        ws.Cell(row, 10).Value = order.TotalPrice;
                        ws.Cell(row, 11).Value = order.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm");
                        row++;
                    }
                }
                grandTotal += order.TotalPrice;
            }

            // Grand total row
            int totalRow = row + 1;
            ws.Cell(totalRow, 9).Value = "GRAND TOTAL";
            ws.Cell(totalRow, 9).Style.Font.Bold = true;
            ws.Cell(totalRow, 10).Value = grandTotal;
            ws.Cell(totalRow, 10).Style.Font.Bold = true;
            ws.Cell(totalRow, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF08A");

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Orders_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
        }

        /// <summary>Supervisor: download Excel sheet of all transactions (description, price, phone, total).</summary>
        [HttpGet("export/transactions")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportTransactions()
        {
            var transactions = await _db.Transactions
                .Include(t => t.Supervisor)
                .OrderByDescending(t => t.CreatedAtUtc)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Transactions");

            var headers = new[] { "Transaction ID", "Supervisor Name", "Description", "Phone Number", "Price", "Created At", "Updated At" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#16A34A");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 2;
            foreach (var t in transactions)
            {
                ws.Cell(row, 1).Value = t.Id.ToString();
                ws.Cell(row, 2).Value = t.Supervisor?.FullName ?? "";
                ws.Cell(row, 3).Value = t.Description;
                ws.Cell(row, 4).Value = t.PhoneNumber;
                ws.Cell(row, 5).Value = t.Price;
                ws.Cell(row, 6).Value = t.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 7).Value = t.UpdatedAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? "";
                row++;
            }

            // Total row
            int totalRow = row + 1;
            ws.Cell(totalRow, 4).Value = "TOTAL";
            ws.Cell(totalRow, 4).Style.Font.Bold = true;
            ws.Cell(totalRow, 5).FormulaA1 = $"=SUM(E2:E{row - 1})";
            ws.Cell(totalRow, 5).Style.Font.Bold = true;
            ws.Cell(totalRow, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF08A");

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Transactions_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
        }
    }
}
