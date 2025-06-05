using Microsoft.EntityFrameworkCore;
using InternalChatbox.Models;
using InternalChatbox.Helpers;

namespace InternalChatbox.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatGroup> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the relationship between ChatMessage and User for Sender and Receiver
            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Receiver)
                .WithMany()
                .HasForeignKey(cm => cm.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure unique index for EmployeeId (nullable)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.EmployeeId)
                .IsUnique()
                .HasFilter("\"EmployeeId\" IS NOT NULL");
        }

        public void Seed()
        {
            // Check if an admin user already exists
            if (!Users.Any(u => u.Role == "Admin"))
            {
                // Create a predefined admin user
                var adminUser = new User
                {
                    Name = "Five",
                    Email = "admin@einfratechsys.com",
                    PasswordHash = PasswordHelper.HashPassword("Admin@123"), // Default password
                    Role = "Admin",
                    Status = "Offline",
                    EmployeeId = "EMP000001",
                    Designation = "Administrator",
                    Location = "Head Office"
                };

                Users.Add(adminUser);
                SaveChanges();
            }
        }
    }
}