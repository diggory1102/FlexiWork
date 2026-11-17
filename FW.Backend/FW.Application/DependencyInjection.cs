using FW.Application.Features.Attendance;
using FW.Application.Features.Auth;
using FW.Application.Features.Chat;
using FW.Application.Features.FaceId;
using FW.Application.Features.Notifications;
using FW.Application.Features.Organization;
using FW.Application.Features.Requests;
using FW.Application.Features.Scheduling;
using FW.Application.Features.Exports;
using FW.Application.Features.Payroll;
using FW.Application.Features.Reports;
using FW.Application.Features.Setup;
using FW.Application.Features.ShiftTasks;
using FW.Application.Features.Timesheets;
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
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<ITimesheetService, TimesheetService>();
        services.AddScoped<IPayrollConfigService, PayrollConfigService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IPayslipService, PayslipService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IViolationReportService, ViolationReportService>();
        services.AddScoped<ILeaveUsageReportService, LeaveUsageReportService>();
        services.AddScoped<ITimesheetGuard, TimesheetGuard>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IFaceProfileService, FaceProfileService>();

        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();

        return services;
    }
}
