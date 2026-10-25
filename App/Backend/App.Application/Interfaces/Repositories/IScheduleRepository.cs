using App.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace App.Application.Interfaces.Repositories
{
    public interface IScheduleRepository
    {
        Task<Schedule?> GetByIdAsync(long id);
        Task<IReadOnlyList<Schedule>> GetSchedulesByDateAndBranchAsync(DateTime workDate, int branchId);
        Task<IReadOnlyList<Schedule>> GetUserSchedulesByDateRangeAsync(int userId, DateTime fromDate, DateTime toDate);
        Task<Schedule?> GetUserScheduleForDateAsync(int userId, DateTime workDate);
        Task<bool> HasShiftConflictAsync(int userId, int shiftId, DateTime workDate);
        Task<long> CreateScheduleAsync(Schedule schedule);
        Task<bool> UpdateStatusAsync(long scheduleId, string status);
    }
}
