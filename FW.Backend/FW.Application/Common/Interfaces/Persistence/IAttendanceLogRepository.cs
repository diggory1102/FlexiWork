using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public interface IAttendanceLogRepository : IGenericRepository<AttendanceLog, long>
{
    /// <summary>Bản ghi chấm công của một ca (mỗi ca tối đa một bản ghi).</summary>
    Task<AttendanceLog?> GetByScheduleIdAsync(long scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Bản ghi chấm công của nhiều ca (bỏ qua ca chưa chấm công).</summary>
    Task<IReadOnlyList<AttendanceLog>> GetByScheduleIdsAsync(IEnumerable<long> scheduleIds, CancellationToken cancellationToken = default);

    /// <summary>Lượt đã check-in nhưng chưa check-out gần nhất của nhân viên (đang trong ca).</summary>
    Task<AttendanceLog?> GetOpenByUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhân viên KHÁC đã check-in bằng cùng thiết bị trong ngày công — dấu hiệu một điện thoại chấm công hộ nhiều người.
    /// </summary>
    Task<IReadOnlyList<int>> GetOtherUsersOnDeviceAsync(string deviceId, DateTime workDate, int excludeUserId,
        CancellationToken cancellationToken = default);
}
