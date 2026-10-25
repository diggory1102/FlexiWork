using App.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<BranchWifi> BranchWifis { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<FaceProfile> FaceProfiles { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<AttendanceLog> AttendanceLogs { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<ShiftTask> ShiftTasks { get; set; }
        public DbSet<ShiftTaskAssignment> ShiftTaskAssignments { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ConversationMember> ConversationMembers { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<MonthlyTimesheet> MonthlyTimesheets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==========================================
            // 1. Tổ chức & Chi nhánh
            // ==========================================
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("branches");
                entity.HasKey(b => b.Id);
                entity.HasIndex(b => b.BranchCode).IsUnique();
                entity.Property(b => b.Latitude).HasPrecision(10, 8);
                entity.Property(b => b.Longitude).HasPrecision(11, 8);
            });

            modelBuilder.Entity<BranchWifi>(entity =>
            {
                entity.ToTable("branch_wifis");
                entity.HasKey(w => w.Id);
                entity.HasOne(w => w.Branch)
                      .WithMany(b => b.BranchWifis)
                      .HasForeignKey(w => w.BranchId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("departments");
                entity.HasKey(d => d.Id);
                entity.HasIndex(d => d.Code).IsUnique();
            });

            modelBuilder.Entity<Position>(entity =>
            {
                entity.ToTable("positions");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.BaseSalaryPerHour).HasPrecision(12, 2);
            });

            // ==========================================
            // 2. Người dùng, Phân quyền & Sinh trắc học
            // ==========================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.EmployeeCode).IsUnique();

                entity.HasOne(u => u.Branch)
                      .WithMany(b => b.Users)
                      .HasForeignKey(u => u.BranchId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(u => u.Department)
                      .WithMany(d => d.Users)
                      .HasForeignKey(u => u.DepartmentId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(u => u.Position)
                      .WithMany(p => p.Users)
                      .HasForeignKey(u => u.PositionId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => r.RoleName).IsUnique();
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasKey(ur => new { ur.UserId, ur.RoleId });

                entity.HasOne(ur => ur.User)
                      .WithMany(u => u.UserRoles)
                      .HasForeignKey(ur => ur.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ur => ur.Role)
                      .WithMany(r => r.UserRoles)
                      .HasForeignKey(ur => ur.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FaceProfile>(entity =>
            {
                entity.ToTable("face_profiles");
                entity.HasKey(fp => fp.Id);
                entity.HasIndex(fp => fp.UserId).IsUnique();

                entity.HasOne(fp => fp.User)
                      .WithOne(u => u.FaceProfile)
                      .HasForeignKey<FaceProfile>(fp => fp.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==========================================
            // 3. Ca kíp & Lịch làm việc
            // ==========================================
            modelBuilder.Entity<Shift>(entity =>
            {
                entity.ToTable("shifts");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.ShiftCode).IsUnique();
                entity.Property(s => s.WorkUnits).HasPrecision(3, 2);
            });

            modelBuilder.Entity<Schedule>(entity =>
            {
                entity.ToTable("schedules");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => new { s.UserId, s.ShiftId, s.WorkDate }).IsUnique();

                entity.HasOne(s => s.User)
                      .WithMany(u => u.Schedules)
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Shift)
                      .WithMany(sh => sh.Schedules)
                      .HasForeignKey(s => s.ShiftId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Branch)
                      .WithMany(b => b.Schedules)
                      .HasForeignKey(s => s.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ==========================================
            // 4. Chấm công Đa lớp (Attendance Logs)
            // ==========================================
            modelBuilder.Entity<AttendanceLog>(entity =>
            {
                entity.ToTable("attendance_logs");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.CheckinLat).HasPrecision(10, 8);
                entity.Property(a => a.CheckinLng).HasPrecision(11, 8);
                entity.Property(a => a.CheckoutLat).HasPrecision(10, 8);
                entity.Property(a => a.CheckoutLng).HasPrecision(11, 8);
                entity.Property(a => a.ActualWorkHours).HasPrecision(4, 2);

                entity.HasOne(a => a.Schedule)
                      .WithMany(s => s.AttendanceLogs)
                      .HasForeignKey(a => a.ScheduleId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(a => a.User)
                      .WithMany(u => u.AttendanceLogs)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Branch)
                      .WithMany(b => b.AttendanceLogs)
                      .HasForeignKey(a => a.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ==========================================
            // 5. Đơn từ & Phép năm
            // ==========================================
            modelBuilder.Entity<LeaveType>(entity =>
            {
                entity.ToTable("leave_types");
                entity.HasKey(lt => lt.Id);
            });

            modelBuilder.Entity<LeaveRequest>(entity =>
            {
                entity.ToTable("leave_requests");
                entity.HasKey(lr => lr.Id);
                entity.Property(lr => lr.DurationDays).HasPrecision(3, 1);

                entity.HasOne(lr => lr.User)
                      .WithMany(u => u.LeaveRequests)
                      .HasForeignKey(lr => lr.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(lr => lr.LeaveType)
                      .WithMany(lt => lt.LeaveRequests)
                      .HasForeignKey(lr => lr.LeaveTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(lr => lr.TargetUser)
                      .WithMany()
                      .HasForeignKey(lr => lr.TargetUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(lr => lr.SourceSchedule)
                      .WithMany()
                      .HasForeignKey(lr => lr.SourceScheduleId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(lr => lr.TargetSchedule)
                      .WithMany()
                      .HasForeignKey(lr => lr.TargetScheduleId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(lr => lr.Approver)
                      .WithMany()
                      .HasForeignKey(lr => lr.ApprovedBy)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<LeaveBalance>(entity =>
            {
                entity.ToTable("leave_balances");
                entity.HasKey(lb => lb.Id);
                entity.HasIndex(lb => new { lb.UserId, lb.Year }).IsUnique();
                entity.Property(lb => lb.TotalDays).HasPrecision(3, 1);
                entity.Property(lb => lb.UsedDays).HasPrecision(3, 1);
                entity.Property(lb => lb.RemainingDays).HasPrecision(3, 1);

                entity.HasOne(lb => lb.User)
                      .WithMany(u => u.LeaveBalances)
                      .HasForeignKey(lb => lb.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==========================================
            // 6. Đầu việc theo ca
            // ==========================================
            modelBuilder.Entity<ShiftTask>(entity =>
            {
                entity.ToTable("shift_tasks");
                entity.HasKey(st => st.Id);

                entity.HasOne(st => st.Shift)
                      .WithMany(sh => sh.ShiftTasks)
                      .HasForeignKey(st => st.ShiftId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ShiftTaskAssignment>(entity =>
            {
                entity.ToTable("shift_task_assignments");
                entity.HasKey(sta => sta.Id);

                entity.HasOne(sta => sta.Task)
                      .WithMany(t => t.Assignments)
                      .HasForeignKey(sta => sta.TaskId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sta => sta.Schedule)
                      .WithMany(s => s.ShiftTaskAssignments)
                      .HasForeignKey(sta => sta.ScheduleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sta => sta.Assignee)
                      .WithMany(u => u.AssignedTasks)
                      .HasForeignKey(sta => sta.AssignedTo)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==========================================
            // 7. Truyền thông nội bộ & Thông báo
            // ==========================================
            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.ToTable("conversations");
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.Branch)
                      .WithMany()
                      .HasForeignKey(c => c.BranchId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ConversationMember>(entity =>
            {
                entity.ToTable("conversation_members");
                entity.HasKey(cm => new { cm.ConversationId, cm.UserId });

                entity.HasOne(cm => cm.Conversation)
                      .WithMany(c => c.Members)
                      .HasForeignKey(cm => cm.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cm => cm.User)
                      .WithMany(u => u.ConversationMembers)
                      .HasForeignKey(cm => cm.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("messages");
                entity.HasKey(m => m.Id);

                entity.HasOne(m => m.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Sender)
                      .WithMany(u => u.Messages)
                      .HasForeignKey(m => m.SenderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("notifications");
                entity.HasKey(n => n.Id);

                entity.HasOne(n => n.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==========================================
            // 8. Bảng công & Lương tháng
            // ==========================================
            modelBuilder.Entity<MonthlyTimesheet>(entity =>
            {
                entity.ToTable("monthly_timesheets");
                entity.HasKey(mt => mt.Id);
                entity.HasIndex(mt => new { mt.UserId, mt.Month, mt.Year }).IsUnique();

                entity.Property(mt => mt.StandardWorkDays).HasPrecision(4, 1);
                entity.Property(mt => mt.ActualWorkDays).HasPrecision(4, 1);
                entity.Property(mt => mt.TotalOvertimeHours).HasPrecision(4, 2);
                entity.Property(mt => mt.PaidLeaveDays).HasPrecision(3, 1);
                entity.Property(mt => mt.UnpaidLeaveDays).HasPrecision(3, 1);
                entity.Property(mt => mt.CalculatedSalary).HasPrecision(14, 2);

                entity.HasOne(mt => mt.User)
                      .WithMany(u => u.MonthlyTimesheets)
                      .HasForeignKey(mt => mt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
