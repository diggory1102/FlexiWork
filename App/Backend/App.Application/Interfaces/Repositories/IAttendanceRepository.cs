using App.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace App.Application.Interfaces.Repositories
{
    public interface IAttendanceRepository
    {
        Task<AttendanceLog?> GetByIdAsync(long id);
        Task<AttendanceLog?> GetTodayAttendanceAsync(int userId, DateTime date);
        Task<IReadOnlyList<AttendanceLog>> GetUserAttendanceHistoryAsync(int userId, DateTime fromDate, DateTime toDate);
        Task<IReadOnlyList<AttendanceLog>> GetBranchAttendanceByDateAsync(int branchId, DateTime date);
        Task<long> RecordCheckinAsync(AttendanceLog log);
        Task<bool> RecordCheckoutAsync(long attendanceId, DateTime checkoutTime, string checkoutImageUrl, 
                                       decimal checkoutLat, decimal checkoutLng, float checkoutDistance, 
                                       string? checkoutBssid, int earlyMinutes, decimal actualWorkHours, 
                                       string status, string? note);
    }
}
