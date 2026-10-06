using App.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace App.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ConversationMember> ConversationMembers { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ActiveKey generation for unique constraint with Soft Delete
            modelBuilder.Entity<User>()
                .Property(u => u.ActiveKey)
                .HasComputedColumnSql("CASE WHEN DeletedAt IS NULL THEN 1 ELSE NULL END", stored: true);

            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.Email, u.ActiveKey })
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.EmployeeCode, u.ActiveKey })
                .IsUnique();

            // Decimal precision configuration
            modelBuilder.Entity<User>()
                .Property(u => u.BaseSalary)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payroll>()
                .Property(p => p.NetSalary)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payroll>()
                .Property(p => p.TotalAllowances)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payroll>()
                .Property(p => p.TotalPenalties)
                .HasColumnType("decimal(18,2)");

            // Configure Delete Behaviors (RESTRICT instead of CASCADE where necessary)
            
            // Attendances
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.User)
                .WithMany(u => u.Attendances)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payrolls
            modelBuilder.Entity<Payroll>()
                .HasOne(p => p.User)
                .WithMany(u => u.Payrolls)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Requests
            modelBuilder.Entity<Request>()
                .HasOne(r => r.User)
                .WithMany(u => u.Requests)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Schedules
            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.User)
                .WithMany(u => u.Schedules)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tasks
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Assignee)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Messages - CASCADE is acceptable here
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Department - User relationships
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Manager)
                .WithMany()
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ConversationMember composite key
            modelBuilder.Entity<ConversationMember>()
                .HasKey(cm => new { cm.ConversationId, cm.UserId });

            // CASCADE for ConversationMembers
            modelBuilder.Entity<ConversationMember>()
                .HasOne(cm => cm.Conversation)
                .WithMany(c => c.Members)
                .HasForeignKey(cm => cm.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConversationMember>()
                .HasOne(cm => cm.User)
                .WithMany()
                .HasForeignKey(cm => cm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
