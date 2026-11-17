using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class LeaveTypeRepository : BaseRepository<LeaveType>, ILeaveTypeRepository
{
    public LeaveTypeRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "leave_types";
    protected override IReadOnlyList<string> SearchColumns => new[] { "`name`" };
    protected override string DefaultOrderBy => "FIELD(`category`, 'leave', 'shift_swap', 'overtime', 'explanation'), `id`";

    public Task<IReadOnlyList<LeaveType>> GetListAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
        QueryAsync<LeaveType>(
            $"{SelectFrom} WHERE {NotDeletedCondition}{(activeOnly ? " AND `is_active` = 1" : string.Empty)} ORDER BY {DefaultOrderBy}",
            null, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `leave_types`
                          WHERE `name` = @Name AND `is_deleted` = 0 AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { Name = name, ExcludeId = excludeId }, cancellationToken);
    }

    public async Task<(int Total, int Open)> CountRequestsAsync(int leaveTypeId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(*) AS Total, COALESCE(SUM(`status` IN @Open), 0) AS Open
            FROM `leave_requests` WHERE `leave_type_id` = @Id AND `is_deleted` = 0
            """;
        var connection = await Session.GetConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<(long Total, decimal Open)>(
            Command(sql, new { Id = leaveTypeId, Open = RequestStatus.Open }, cancellationToken));
        return ((int)row.Total, (int)row.Open);
    }
}

public class LeaveRequestRepository : BaseRepository<LeaveRequest>, ILeaveRequestRepository
{
    public LeaveRequestRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "leave_requests";

    // r.* ánh xạ sang thuộc tính của LeaveRequest nhờ MatchNamesWithUnderscores (DapperConfig)
    private const string DetailSelect = """
        SELECT r.*,
               u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`avatar_url` AS AvatarUrl,
               b.`name` AS BranchName,
               t.`name` AS LeaveTypeName, t.`category` AS Category, t.`is_paid` AS IsPaid, t.`deducts_balance` AS DeductsBalance,
               tu.`employee_code` AS TargetEmployeeCode, tu.`full_name` AS TargetFullName, tu.`avatar_url` AS TargetAvatarUrl,
               ss.`work_date` AS SourceWorkDate, ssh.`name` AS SourceShiftName, ssh.`start_time` AS SourceStartTime,
               ssh.`end_time` AS SourceEndTime, ss.`status` AS SourceScheduleStatus,
               ts.`work_date` AS TargetWorkDate, tsh.`name` AS TargetShiftName, tsh.`start_time` AS TargetStartTime,
               tsh.`end_time` AS TargetEndTime, ts.`status` AS TargetScheduleStatus,
               al.`work_date` AS LogWorkDate, al.`checkin_time` AS LogCheckinTime, al.`checkout_time` AS LogCheckoutTime,
               al.`late_minutes` AS LogLateMinutes, al.`early_minutes` AS LogEarlyMinutes, al.`status` AS LogStatus,
               alsh.`name` AS LogShiftName,
               ap.`full_name` AS ApproverName
        """;

    private const string DetailFrom = """
        FROM `leave_requests` r
        JOIN `users` u ON u.`id` = r.`user_id`
        JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
        LEFT JOIN `branches` b ON b.`id` = r.`branch_id`
        LEFT JOIN `users` tu ON tu.`id` = r.`target_user_id`
        LEFT JOIN `schedules` ss ON ss.`id` = r.`source_schedule_id`
        LEFT JOIN `shifts` ssh ON ssh.`id` = ss.`shift_id`
        LEFT JOIN `schedules` ts ON ts.`id` = r.`target_schedule_id`
        LEFT JOIN `shifts` tsh ON tsh.`id` = ts.`shift_id`
        LEFT JOIN `attendance_logs` al ON al.`id` = r.`attendance_log_id`
        LEFT JOIN `schedules` als ON als.`id` = al.`schedule_id`
        LEFT JOIN `shifts` alsh ON alsh.`id` = als.`shift_id`
        LEFT JOIN `users` ap ON ap.`id` = r.`approved_by`
        """;

    static LeaveRequestRepository() => DapperConfig.Configure();

    public async Task<PaginatedResult<LeaveRequestDetail>> GetDetailsPagedAsync(LeaveRequestQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "r.`is_deleted` = 0" };
        var parameters = new DynamicParameters();

        if (query.UserId.HasValue)
        {
            conditions.Add("r.`user_id` = @UserId");
            parameters.Add("UserId", query.UserId);
        }
        if (query.TargetUserId.HasValue)
        {
            conditions.Add("r.`target_user_id` = @TargetUserId");
            parameters.Add("TargetUserId", query.TargetUserId);
        }
        if (query.BranchId.HasValue)
        {
            conditions.Add("r.`branch_id` = @BranchId");
            parameters.Add("BranchId", query.BranchId);
        }
        if (query.Statuses is { Count: > 0 })
        {
            conditions.Add("r.`status` IN @Statuses");
            parameters.Add("Statuses", query.Statuses);
        }
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            conditions.Add("t.`category` = @Category");
            parameters.Add("Category", query.Category);
        }
        if (query.FromDate.HasValue)
        {
            conditions.Add("r.`end_date` >= @FromDate");
            parameters.Add("FromDate", query.FromDate.Value.Date);
        }
        if (query.ToDate.HasValue)
        {
            conditions.Add("r.`start_date` <= @ToDate");
            parameters.Add("ToDate", query.ToDate.Value.Date);
        }
        AddSearchCondition(query.Search, new[] { "u.`full_name`", "u.`employee_code`" }, conditions, parameters);

        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.NormalizedPageSize);
        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT COUNT(*) FROM `leave_requests` r
            JOIN `users` u ON u.`id` = r.`user_id` JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
            WHERE {where};
            {DetailSelect} {DetailFrom}
            WHERE {where}
            ORDER BY r.`created_at` DESC, r.`id` DESC
            LIMIT @Take OFFSET @Skip;
            """;

        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(Command(sql, parameters, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<LeaveRequestDetail>()).ToList();
        return PaginatedResult<LeaveRequestDetail>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }

    public Task<LeaveRequestDetail?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<LeaveRequestDetail>(
            $"{DetailSelect} {DetailFrom} WHERE r.`id` = @Id AND r.`is_deleted` = 0", new { Id = id }, cancellationToken);

    public Task<bool> HasOverlappingLeaveAsync(int userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `leave_requests` r JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
                          WHERE r.`user_id` = @UserId AND r.`is_deleted` = 0 AND t.`category` = @Category
                            AND r.`status` IN @Blocking AND r.`start_date` <= @EndDate AND r.`end_date` >= @StartDate)
            """;
        return ExecuteScalarAsync<bool>(sql, new
        {
            UserId = userId, Category = RequestCategory.Leave, Blocking = RequestStatus.Blocking,
            StartDate = startDate.Date, EndDate = endDate.Date
        }, cancellationToken);
    }

    public Task<bool> HasOverlappingOvertimeAsync(int userId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `leave_requests` r JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
                          WHERE r.`user_id` = @UserId AND r.`is_deleted` = 0 AND t.`category` = @Category
                            AND r.`status` IN @Blocking AND r.`start_at` < @EndAt AND r.`end_at` > @StartAt)
            """;
        return ExecuteScalarAsync<bool>(sql, new
        {
            UserId = userId, Category = RequestCategory.Overtime, Blocking = RequestStatus.Blocking, StartAt = startAt, EndAt = endAt
        }, cancellationToken);
    }

    public Task<bool> HasOpenSwapForSchedulesAsync(IReadOnlyCollection<long> scheduleIds, int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (scheduleIds.Count == 0) return Task.FromResult(false);
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `leave_requests`
                          WHERE `is_deleted` = 0 AND `status` IN @Open AND (@ExcludeId IS NULL OR `id` <> @ExcludeId)
                            AND (`source_schedule_id` IN @Ids OR `target_schedule_id` IN @Ids))
            """;
        return ExecuteScalarAsync<bool>(sql, new { Open = RequestStatus.Open, ExcludeId = excludeId, Ids = scheduleIds }, cancellationToken);
    }

    public Task<bool> HasActiveExplanationAsync(long attendanceLogId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `leave_requests`
                          WHERE `attendance_log_id` = @LogId AND `is_deleted` = 0 AND `status` IN @Blocking)
            """;
        return ExecuteScalarAsync<bool>(sql, new { LogId = attendanceLogId, Blocking = RequestStatus.Blocking }, cancellationToken);
    }

    public Task<decimal> GetPendingDeductibleDaysAsync(int userId, int year, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COALESCE(SUM(r.`duration_days`), 0) FROM `leave_requests` r JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
            WHERE r.`user_id` = @UserId AND r.`is_deleted` = 0 AND t.`deducts_balance` = 1 AND r.`status` = @Pending
              AND r.`start_date` BETWEEN @From AND @To
            """;
        return ExecuteScalarAsync<decimal>(sql, new
        {
            UserId = userId, Pending = RequestStatus.Pending, From = new DateTime(year, 1, 1), To = new DateTime(year, 12, 31)
        }, cancellationToken);
    }

    public async Task<bool> TryCompleteReviewAsync(int id, string status, int reviewerId, DateTime reviewedAt, string? note,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE `leave_requests`
            SET `status` = @Status, `approved_by` = @ReviewerId, `approved_at` = @ReviewedAt, `approver_note` = @Note
            WHERE `id` = @Id AND `status` = @Pending AND `is_deleted` = 0
            """;
        return await ExecuteAsync(sql, new
        {
            Id = id, Status = status, ReviewerId = reviewerId, ReviewedAt = reviewedAt, Note = note, Pending = RequestStatus.Pending
        }, cancellationToken) == 1;
    }

    public Task<IReadOnlyList<ApprovedOvertime>> GetApprovedOvertimeAsync(DateTime fromDate, DateTime toDate, IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return Task.FromResult<IReadOnlyList<ApprovedOvertime>>(Array.Empty<ApprovedOvertime>());
        const string sql = """
            SELECT r.`user_id` AS UserId, r.`start_at` AS StartAt, r.`end_at` AS EndAt, COALESCE(r.`duration_hours`, 0) AS DurationHours
            FROM `leave_requests` r JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
            WHERE r.`user_id` IN @Ids AND r.`is_deleted` = 0 AND t.`category` = @Category AND r.`status` = @Approved
              AND r.`start_date` BETWEEN @FromDate AND @ToDate AND r.`start_at` IS NOT NULL AND r.`end_at` IS NOT NULL
            ORDER BY r.`start_at`
            """;
        return QueryAsync<ApprovedOvertime>(sql, new
        {
            Ids = userIds, Category = RequestCategory.Overtime, Approved = RequestStatus.Approved, FromDate = fromDate.Date, ToDate = toDate.Date
        }, cancellationToken);
    }

    public Task<IReadOnlyList<long>> GetApprovedExplanationLogIdsAsync(IReadOnlyCollection<long> attendanceLogIds, CancellationToken cancellationToken = default)
    {
        if (attendanceLogIds.Count == 0) return Task.FromResult<IReadOnlyList<long>>(Array.Empty<long>());
        const string sql = """
            SELECT DISTINCT r.`attendance_log_id` FROM `leave_requests` r
            WHERE r.`attendance_log_id` IN @Ids AND r.`is_deleted` = 0 AND r.`status` = @Approved
            """;
        return QueryAsync<long>(sql, new { Ids = attendanceLogIds, Approved = RequestStatus.Approved }, cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovedLeave>> GetApprovedLeavesAsync(IEnumerable<int> userIds, DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<ApprovedLeave>();
        const string sql = """
            SELECT r.`user_id` AS UserId, r.`start_date` AS StartDate, r.`end_date` AS EndDate, t.`name` AS LeaveTypeName, t.`is_paid` AS IsPaid
            FROM `leave_requests` r JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
            WHERE r.`user_id` IN @Ids AND r.`is_deleted` = 0 AND t.`category` = @Category AND r.`status` = @Approved
              AND r.`start_date` <= @ToDate AND r.`end_date` >= @FromDate
            """;
        return await QueryAsync<ApprovedLeave>(sql, new
        {
            Ids = ids, Category = RequestCategory.Leave, Approved = RequestStatus.Approved, FromDate = fromDate.Date, ToDate = toDate.Date
        }, cancellationToken);
    }
}

public class LeaveBalanceRepository : BaseRepository<LeaveBalance>, ILeaveBalanceRepository
{
    public LeaveBalanceRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "leave_balances";

    public Task<LeaveBalance?> GetByUserAndYearAsync(int userId, int year, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<LeaveBalance>($"{SelectFrom} WHERE `user_id` = @UserId AND `year` = @Year",
            new { UserId = userId, Year = year }, cancellationToken);

    public Task EnsureExistsAsync(int userId, int year, decimal defaultTotalDays, CancellationToken cancellationToken = default)
    {
        // UNIQUE(user_id, year): đã có thì giữ nguyên
        const string sql = """
            INSERT INTO `leave_balances` (`user_id`, `year`, `total_days`, `used_days`, `remaining_days`)
            VALUES (@UserId, @Year, @Total, 0, @Total)
            ON DUPLICATE KEY UPDATE `user_id` = `user_id`
            """;
        return ExecuteAsync(sql, new { UserId = userId, Year = year, Total = defaultTotalDays }, cancellationToken);
    }

    public async Task<bool> TryDeductAsync(int userId, int year, decimal days, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE `leave_balances`
            SET `used_days` = `used_days` + @Days, `remaining_days` = `remaining_days` - @Days
            WHERE `user_id` = @UserId AND `year` = @Year AND `remaining_days` >= @Days
            """;
        return await ExecuteAsync(sql, new { UserId = userId, Year = year, Days = days }, cancellationToken) == 1;
    }

    public async Task<bool> TryAdjustTotalAsync(int userId, int year, decimal deltaDays, decimal maxTotalDays,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE `leave_balances`
            SET `total_days` = `total_days` + @Delta, `remaining_days` = `remaining_days` + @Delta
            WHERE `user_id` = @UserId AND `year` = @Year
              AND `total_days` + @Delta >= `used_days` AND `total_days` + @Delta <= @Max
            """;
        return await ExecuteAsync(sql, new { UserId = userId, Year = year, Delta = deltaDays, Max = maxTotalDays }, cancellationToken) == 1;
    }

    public async Task<PaginatedResult<LeaveBalanceRow>> GetRowsPagedAsync(LeaveBalanceQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "u.`is_deleted` = 0", "u.`status` <> @Left" };
        var parameters = new DynamicParameters(new
        {
            Left = UserStatus.Left, query.Year, Pending = RequestStatus.Pending,
            From = new DateTime(query.Year, 1, 1), To = new DateTime(query.Year, 12, 31)
        });
        if (query.BranchId.HasValue)
        {
            conditions.Add("u.`branch_id` = @BranchId");
            parameters.Add("BranchId", query.BranchId);
        }
        if (query.UserId.HasValue)
        {
            conditions.Add("u.`id` = @UserId");
            parameters.Add("UserId", query.UserId);
        }
        AddSearchCondition(query.Search, new[] { "u.`full_name`", "u.`employee_code`" }, conditions, parameters);
        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.NormalizedPageSize);

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT COUNT(*) FROM `users` u WHERE {where};
            SELECT u.`id` AS UserId, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`avatar_url` AS AvatarUrl,
                   u.`branch_id` AS BranchId, b.`name` AS BranchName,
                   lb.`total_days` AS TotalDays, lb.`used_days` AS UsedDays, lb.`remaining_days` AS RemainingDays,
                   COALESCE((SELECT SUM(r.`duration_days`) FROM `leave_requests` r JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
                             WHERE r.`user_id` = u.`id` AND r.`is_deleted` = 0 AND t.`deducts_balance` = 1 AND r.`status` = @Pending
                               AND r.`start_date` BETWEEN @From AND @To), 0) AS PendingDays
            FROM `users` u
            LEFT JOIN `branches` b ON b.`id` = u.`branch_id`
            LEFT JOIN `leave_balances` lb ON lb.`user_id` = u.`id` AND lb.`year` = @Year
            WHERE {where}
            ORDER BY u.`employee_code`
            LIMIT @Take OFFSET @Skip;
            """;

        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(Command(sql, parameters, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<LeaveBalanceRow>()).ToList();
        return PaginatedResult<LeaveBalanceRow>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }
}

public class LeaveBalanceAdjustmentRepository : BaseRepository<LeaveBalanceAdjustment>, ILeaveBalanceAdjustmentRepository
{
    public LeaveBalanceAdjustmentRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "leave_balance_adjustments";

    public Task<IReadOnlyList<LeaveBalanceAdjustmentDetail>> GetByUserAndYearAsync(int userId, int year, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT a.`id` AS Id, a.`user_id` AS UserId, a.`year` AS Year, a.`delta_days` AS DeltaDays, a.`reason` AS Reason,
                   a.`created_by` AS CreatedBy, a.`created_at` AS CreatedAt, c.`full_name` AS CreatedByName
            FROM `leave_balance_adjustments` a LEFT JOIN `users` c ON c.`id` = a.`created_by`
            WHERE a.`user_id` = @UserId AND a.`year` = @Year
            ORDER BY a.`created_at` DESC, a.`id` DESC
            """;
        return QueryAsync<LeaveBalanceAdjustmentDetail>(sql, new { UserId = userId, Year = year }, cancellationToken);
    }
}
