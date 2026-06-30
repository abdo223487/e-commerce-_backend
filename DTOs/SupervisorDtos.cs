namespace MarketplaceApi.DTOs
{
    public class UserListItemDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public decimal Coins { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ProfitsResponseDto
    {
        public decimal OrdersRevenue { get; set; }
        public decimal TransactionsRevenue { get; set; }
        public decimal TotalProfits { get; set; }
    }
}
