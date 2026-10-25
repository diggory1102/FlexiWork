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
    public class AttendanceRepository : IAttendanceRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AttendanceRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<AttendanceLog?> GetByIdAsync(long id)
        {
            const string sql = @"
                SELECT id, schedule_id AS ScheduleId, user_id AS UserId, branch_id AS BranchId,
                       checkin_time AS CheckinTime, checkin_image_url AS CheckinImageUrl,
                       checkin_lat AS CheckinLat, checkin_lng AS CheckinLng, checkin_distance AS CheckinDistance,
                       checkin_bssid AS CheckinBssid, is_face_matched AS IsFaceMatched, face_match_confidence AS FaceMatchConfidence,
                       checkout_time AS CheckoutTime, checkout_image_url AS CheckoutImageUrl,
                       checkout_lat AS CheckoutLat, checkout_lng AS CheckoutLng, checkout_distance AS CheckoutDistance,
                       checkout_bssid AS CheckoutBssid, late_minutes AS LateMinutes, early_minutes AS EarlyMinutes,
                       actual_work_hours AS ActualWorkHours, status, note, created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM attendance_logs
                WHERE id = @Id
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<AttendanceLog>(sql, new { Id = id });
        }

        public async Task<AttendanceLog?> GetTodayAttendanceAsync(int userId, DateTime date)
        {
            const string sql = @"
                SELECT id, schedule_id AS ScheduleId, user_id AS UserId, branch_id AS BranchId,
                       checkin_time AS CheckinTime, checkin_image_url AS CheckinImageUrl,
                       checkin_lat AS CheckinLat, checkin_lng AS CheckinLng, checkin_distance AS CheckinDistance,
                       checkin_bssid AS CheckinBssid, is_face_matched AS IsFaceMatched, face_match_confidence AS FaceMatchConfidence,
                       checkout_time AS CheckoutTime, checkout_image_url AS CheckoutImageUrl,
                       checkout_lat AS CheckoutLat, checkout_lng AS CheckoutLng, checkout_distance AS CheckoutDistance,
                       checkout_bssid AS CheckoutBssid, late_minutes AS LateMinutes, early_minutes AS EarlyMinutes,
                       actual_work_hours AS ActualWorkHours, status, note, created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM attendance_logs
                WHERE user_id = @UserId AND DATE(checkin_time) = @CheckinDate
                ORDER BY checkin_time DESC
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<AttendanceLog>(sql, new
            {
                UserId = userId,
                CheckinDate = date.Date
            });
        }

        public async Task<IReadOnlyList<AttendanceLog>> GetUserAttendanceHistoryAsync(int userId, DateTime fromDate, DateTime toDate)
        {
            const string sql = @"
                SELECT a.id, a.schedule_id AS ScheduleId, a.user_id AS UserId, a.branch_id AS BranchId,
                       a.checkin_time AS CheckinTime, a.checkin_image_url AS CheckinImageUrl,
                       a.checkin_lat AS CheckinLat, a.checkin_lng AS CheckinLng, a.checkin_distance AS CheckinDistance,
                       a.checkin_bssid AS CheckinBssid, a.is_face_matched AS IsFaceMatched, a.face_match_confidence AS FaceMatchConfidence,
                       a.checkout_time AS CheckoutTime, a.checkout_image_url AS CheckoutImageUrl,
                       a.checkout_lat AS CheckoutLat, a.checkout_lng AS CheckoutLng, a.checkout_distance AS CheckoutDistance,
                       a.checkout_bssid AS CheckoutBssid, a.late_minutes AS LateMinutes, a.early_minutes AS EarlyMinutes,
                       a.actual_work_hours AS ActualWorkHours, a.status, a.note, a.created_at AS CreatedAt,
                       b.id, b.branch_code AS BranchCode, b.name
                FROM attendance_logs a
                INNER JOIN branches b ON a.branch_id = b.id
                WHERE a.user_id = @UserId 
                  AND a.checkin_time >= @FromDate 
                  AND a.checkin_time <= @ToDate
                ORDER BY a.checkin_time DESC;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<AttendanceLog, Branch, AttendanceLog>(
                sql,
                (log, branch) =>
                {
                    log.Branch = branch;
                    return log;
                },
                new { UserId = userId, FromDate = fromDate, ToDate = toDate },
                splitOn: "id"
            );

            return result.ToList();
        }

        public async Task<IReadOnlyList<AttendanceLog>> GetBranchAttendanceByDateAsync(int branchId, DateTime date)
        {
            const string sql = @"
                SELECT a.id, a.schedule_id AS ScheduleId, a.user_id AS UserId, a.branch_id AS BranchId,
                       a.checkin_time AS CheckinTime, a.checkin_image_url AS CheckinImageUrl,
                       a.checkout_time AS CheckoutTime, a.checkout_image_url AS CheckoutImageUrl,
                       a.late_minutes AS LateMinutes, a.early_minutes AS EarlyMinutes,
                       a.actual_work_hours AS ActualWorkHours, a.status, a.note,
                       u.id, u.employee_code AS EmployeeCode, u.full_name AS FullName, u.avatar_url AS AvatarUrl
                FROM attendance_logs a
                INNER JOIN users u ON a.user_id = u.id
                WHERE a.branch_id = @BranchId AND DATE(a.checkin_time) = @Date
                ORDER BY a.checkin_time DESC;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<AttendanceLog, User, AttendanceLog>(
                sql,
                (log, user) =>
                {
                    log.User = user;
                    return log;
                },
                new { BranchId = branchId, Date = date.Date },
                splitOn: "id"
            );

            return result.ToList();
        }

        public async Task<long> RecordCheckinAsync(AttendanceLog log)
        {
            const string sql = @"
                INSERT INTO attendance_logs (
                    schedule_id, user_id, branch_id, checkin_time, checkin_image_url,
                    checkin_lat, checkin_lng, checkin_distance, checkin_bssid,
                    is_face_matched, face_match_confidence, late_minutes, early_minutes,
                    actual_work_hours, status, note, created_at
                ) VALUES (
                    @ScheduleId, @UserId, @BranchId, @CheckinTime, @CheckinImageUrl,
                    @CheckinLat, @CheckinLng, @CheckinDistance, @CheckinBssid,
                    @IsFaceMatched, @FaceMatchConfidence, @LateMinutes, @EarlyMinutes,
                    @ActualWorkHours, @Status, @Note, NOW()
                );
                SELECT LAST_INSERT_ID();";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.ExecuteScalarAsync<long>(sql, log);
        }

        public async Task<bool> RecordCheckoutAsync(long attendanceId, DateTime checkoutTime, string checkoutImageUrl, 
                                                   decimal checkoutLat, decimal checkoutLng, float checkoutDistance, 
                                                   string? checkoutBssid, int earlyMinutes, decimal actualWorkHours, 
                                                   string status, string? note)
        {
            const string sql = @"
                UPDATE attendance_logs
                SET checkout_time = @CheckoutTime,
                    checkout_image_url = @CheckoutImageUrl,
                    checkout_lat = @CheckoutLat,
                    checkout_lng = @CheckoutLng,
                    checkout_distance = @CheckoutDistance,
                    checkout_bssid = @CheckoutBssid,
                    early_minutes = @EarlyMinutes,
                    actual_work_hours = @ActualWorkHours,
                    status = @Status,
                    note = COALESCE(@Note, note),
                    updated_at = NOW()
                WHERE id = @Id;";

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = attendanceId,
                CheckoutTime = checkoutTime,
                CheckoutImageUrl = checkoutImageUrl,
                CheckoutLat = checkoutLat,
                CheckoutLng = checkoutLng,
                CheckoutDistance = checkoutDistance,
                CheckoutBssid = checkoutBssid,
                EarlyMinutes = earlyMinutes,
                ActualWorkHours = actualWorkHours,
                Status = status,
                Note = note
            });

            return rows > 0;
        }
    }
}
