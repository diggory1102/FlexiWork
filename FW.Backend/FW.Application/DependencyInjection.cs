using FW.Application.Features.Attendance;
using FW.Application.Features.Auth;
using FW.Application.Features.FaceId;
using FW.Application.Features.Organization;
using FW.Application.Features.Requests;
using FW.Application.Features.Scheduling;
using FW.Application.Features.Setup;
using FW.Application.Features.ShiftTasks;
using FW.Application.Features.Users;
using Microsoft.Extensions.DependencyInjection;

namespace FW.Application;

/// <summary>
/// Đăng ký các dịch vụ thuộc tầng Ứng dụng (Application Layer).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionStampService, SessionStampService>();

        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IAddressLookupService, AddressLookupService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPositionService, PositionService>();

        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IAttendancePolicyService, AttendancePolicyService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAttendanceLogService, AttendanceLogService>();
        services.AddScoped<ILeaveTypeService, LeaveTypeService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        services.AddScoped<IRequestScheduleChecker, RequestScheduleChecker>();
        services.AddScoped<ILeaveApprovalService, LeaveApprovalService>();
        services.AddScoped<ILeaveBalanceService, LeaveBalanceService>();
        services.AddScoped<IShiftTaskService, ShiftTaskService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IFaceProfileService, FaceProfileService>();

        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();

        return services;
    }
}
