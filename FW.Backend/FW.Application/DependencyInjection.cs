using FW.Application.Features.Assistant;
using FW.Application.Features.Attendance;
using FW.Application.Features.Attrition;
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
        services.AddSingleton<ILiveSessionRegistry, LiveSessionRegistry>();

        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IAddressLookupService, AddressLookupService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPositionService, PositionService>();

        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IAttendancePolicyService, AttendancePolicyService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IShiftStaffingService, ShiftStaffingService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAttendanceLogService, AttendanceLogService>();
        services.AddScoped<IAttendanceAdjustmentService, AttendanceAdjustmentService>();
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

        AddAssistant(services);

        return services;
    }

    /// <summary>Trợ lý nhân sự AI (Task 7.1 – 7.2) và cảnh báo nguy cơ nghỉ việc (Task 7.4).</summary>
    private static void AddAssistant(IServiceCollection services)
    {
        // Giá trị cấu hình (mục "Assistant", "Attrition") được nạp ở AddInfrastructure; ở đây chỉ bảo đảm có giá trị mặc định
        services.AddOptions<AssistantOptions>();
        services.AddOptions<AttritionOptions>();

        services.AddScoped<IHrDataService, HrDataService>();
        services.AddScoped<ILeaveRequestSubmitter, LeaveRequestSubmitter>();

        // Công cụ của trợ lý — mỗi công cụ tự khai báo vai trò được dùng (AllowedRoles)
        services.AddScoped<IAssistantTool, GetMyScheduleTool>();
        services.AddScoped<IAssistantTool, GetMyTimesheetTool>();
        services.AddScoped<IAssistantTool, GetMyLeaveBalanceTool>();
        services.AddScoped<IAssistantTool, GetMyRequestsTool>();
        services.AddScoped<IAssistantTool, DraftLeaveRequestTool>();
        services.AddScoped<IAssistantTool, DraftShiftSwapTool>();
        services.AddScoped<IAssistantTool, DraftOvertimeRequestTool>();
        services.AddScoped<IAssistantTool, DraftExplanationRequestTool>();
        services.AddScoped<IAssistantTool, GetPendingRequestsTool>();
        services.AddScoped<IAssistantTool, GetAttritionAlertsTool>();

        services.AddScoped<DraftService>();
        services.AddScoped<AssistantService>();

        services.AddScoped<AttritionScoringService>();
        services.AddScoped<AttritionQueryService>();
        services.AddScoped<AttritionNarrator>();
    }
}
