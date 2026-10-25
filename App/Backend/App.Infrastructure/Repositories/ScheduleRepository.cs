using App.Application.Interfaces.Data;
using App.Application.Interfaces.Repositories;
using App.Domain.Entities;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace App.Infrastructure.Repositories
{
    public class ScheduleRepository : IScheduleRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ScheduleRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Schedule?> GetByIdAsync(long id)
        {
            const string sql = @"
                SELECT s.id, s.user_id AS UserId, s.shift_id AS ShiftId, s.branch_id AS BranchId, 
                       s.work_date AS WorkDate, s.status, s.is_deleted AS IsDeleted, s.created_at AS CreatedAt,
                       sh.id, sh.shift_code AS ShiftCode, sh.name, sh.start_time AS StartTime, 
                       sh.end_time AS EndTime, sh.break_minutes AS BreakMinutes, 
                       sh.work_units AS WorkUnits, sh.is_night_shift AS IsNightShift, sh.is_cross_day AS IsCrossDay
                FROM schedules s
                INNER JOIN shifts sh ON s.shift_id = sh.id
                WHERE s.id = @Id AND s.is_deleted = 0
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<Schedule, Shift, Schedule>(
                sql,
                (schedule, shift) =>
                {
                    schedule.Shift = shift;
                    return schedule;
                },
                new { Id = id },
                splitOn: "id"
            );

            return result.FirstOrDefault();
        }

        public async Task<IReadOnlyList<Schedule>> GetSchedulesByDateAndBranchAsync(DateTime workDate, int branchId)
        {
            const string sql = @"
                SELECT s.id, s.user_id AS UserId, s.shift_id AS ShiftId, s.branch_id AS BranchId, 
                       s.work_date AS WorkDate, s.status, s.is_deleted AS IsDeleted, s.created_at AS CreatedAt,
                       u.id, u.employee_code AS EmployeeCode, u.full_name AS FullName, u.avatar_url AS AvatarUrl,
                       sh.id, sh.shift_code AS ShiftCode, sh.name, sh.start_time AS StartTime, sh.end_time AS EndTime
                FROM schedules s
                INNER JOIN users u ON s.user_id = u.id
                INNER JOIN shifts sh ON s.shift_id = sh.id
                WHERE s.work_date = @WorkDate AND s.branch_id = @BranchId AND s.is_deleted = 0
                ORDER BY sh.start_time ASC;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<Schedule, User, Shift, Schedule>(
                sql,
                (schedule, user, shift) =>
                {
                    schedule.User = user;
                    schedule.Shift = shift;
                    return schedule;
                },
                new { WorkDate = workDate.Date, BranchId = branchId },
                splitOn: "id,id"
            );

            return result.ToList();
        }

        public async Task<IReadOnlyList<Schedule>> GetUserSchedulesByDateRangeAsync(int userId, DateTime fromDate, DateTime toDate)
        {
            const string sql = @"
                SELECT s.id, s.user_id AS UserId, s.shift_id AS ShiftId, s.branch_id AS BranchId, 
                       s.work_date AS WorkDate, s.status, s.is_deleted AS IsDeleted, s.created_at AS CreatedAt,
                       sh.id, sh.shift_code AS ShiftCode, sh.name, sh.start_time AS StartTime, sh.end_time AS EndTime,
                       b.id, b.branch_code AS BranchCode, b.name
                FROM schedules s
                INNER JOIN shifts sh ON s.shift_id = sh.id
                INNER JOIN branches b ON s.branch_id = b.id
                WHERE s.user_id = @UserId 
                  AND s.work_date >= @FromDate 
                  AND s.work_date <= @ToDate 
                  AND s.is_deleted = 0
                ORDER BY s.work_date ASC, sh.start_time ASC;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<Schedule, Shift, Branch, Schedule>(
                sql,
                (schedule, shift, branch) =>
                {
                    schedule.Shift = shift;
                    schedule.Branch = branch;
                    return schedule;
                },
                new { UserId = userId, FromDate = fromDate.Date, ToDate = toDate.Date },
                splitOn: "id,id"
            );

            return result.ToList();
        }

        public async Task<Schedule?> GetUserScheduleForDateAsync(int userId, DateTime workDate)
        {
            const string sql = @"
                SELECT s.id, s.user_id AS UserId, s.shift_id AS ShiftId, s.branch_id AS BranchId, 
                       s.work_date AS WorkDate, s.status, s.is_deleted AS IsDeleted, s.created_at AS CreatedAt,
                       sh.id, sh.shift_code AS ShiftCode, sh.name, sh.start_time AS StartTime, sh.end_time AS EndTime
                FROM schedules s
                INNER JOIN shifts sh ON s.shift_id = sh.id
                WHERE s.user_id = @UserId AND s.work_date = @WorkDate AND s.is_deleted = 0
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<Schedule, Shift, Schedule>(
                sql,
                (schedule, shift) =>
                {
                    schedule.Shift = shift;
                    return schedule;
                },
                new { UserId = userId, WorkDate = workDate.Date },
                splitOn: "id"
            );

            return result.FirstOrDefault();
        }

        public async Task<bool> HasShiftConflictAsync(int userId, int shiftId, DateTime workDate)
        {
            const string sql = @"
                SELECT COUNT(1) 
                FROM schedules 
                WHERE user_id = @UserId 
                  AND shift_id = @ShiftId 
                  AND work_date = @WorkDate 
                  AND is_deleted = 0;";

            using var connection = _connectionFactory.CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(sql, new
            {
                UserId = userId,
                ShiftId = shiftId,
                WorkDate = workDate.Date
            });

            return count > 0;
        }

        public async Task<long> CreateScheduleAsync(Schedule schedule)
        {
            const string sql = @"
                INSERT INTO schedules (user_id, shift_id, branch_id, work_date, status, is_deleted, created_at)
                VALUES (@UserId, @ShiftId, @BranchId, @WorkDate, @Status, 0, NOW());
                SELECT LAST_INSERT_ID();";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.ExecuteScalarAsync<long>(sql, new
            {
                schedule.UserId,
                schedule.ShiftId,
                schedule.BranchId,
                WorkDate = schedule.WorkDate.Date,
                schedule.Status
            });
        }

        public async Task<bool> UpdateStatusAsync(long scheduleId, string status)
        {
            const string sql = @"
                UPDATE schedules 
                SET status = @Status 
                WHERE id = @Id AND is_deleted = 0;";

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.ExecuteAsync(sql, new { Id = scheduleId, Status = status });
            return rows > 0;
        }
    }
}
