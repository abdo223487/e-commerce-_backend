using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Models;

namespace MarketplaceApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<CustomOrder> CustomOrders => Set<CustomOrder>();
        public DbSet<CustomOrderMessage> CustomOrderMessages => Set<CustomOrderMessage>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<TransactionType> TransactionTypes => Set<TransactionType>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.PhoneNumber).IsUnique();
                entity.Property(u => u.PhoneNumber).IsRequired();
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).HasDefaultValue(UserRole.User);
                entity.Ignore(u => u.IsUser);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasOne(rt => rt.User)
                      .WithMany()
                      .HasForeignKey(rt => rt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Name).IsUnique();
                entity.HasMany(c => c.Products)
                      .WithOne(p => p.Category)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.Name);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasOne(o => o.User)
                      .WithMany()
                      .HasForeignKey(o => o.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(o => o.Items)
                      .WithOne(i => i.Order)
                      .HasForeignKey(i => i.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasOne(i => i.Product)
                      .WithMany()
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<OrderStatusHistory>(entity =>
            {
                entity.HasOne(h => h.Order)
                      .WithMany(o => o.StatusHistory)
                      .HasForeignKey(h => h.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.ChangedByUser)
                      .WithMany()
                      .HasForeignKey(h => h.ChangedByUserId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasIndex(h => h.OrderId);
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CustomOrder>(entity =>
            {
                entity.HasOne(co => co.User)
                      .WithMany()
                      .HasForeignKey(co => co.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(co => co.Messages)
                      .WithOne(m => m.CustomOrder)
                      .HasForeignKey(m => m.CustomOrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasOne(t => t.Supervisor)
                      .WithMany()
                      .HasForeignKey(t => t.SupervisorId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Type)
                      .WithMany(tt => tt.Transactions)
                      .HasForeignKey(t => t.TypeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TransactionType>(entity =>
            {
                entity.HasIndex(tt => tt.Name).IsUnique();
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasOne(a => a.Actor)
                      .WithMany()
                      .HasForeignKey(a => a.ActorId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
