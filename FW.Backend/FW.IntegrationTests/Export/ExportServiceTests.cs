using ClosedXML.Excel;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Exports;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Services.Export;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.Export;

/// <summary>
/// Xuất Excel (Task 5.7) trên MySQL thật: bảng công theo phạm vi vai trò (kể cả hơn một trang dữ liệu) và bảng lương của kỳ.
/// Đồng hồ cố định 10/06/2038; dữ liệu ở các tháng của năm 2038 để không lẫn với test khác. Người dùng 2 = Quản lý chi nhánh 1, 4 = HR.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExportServiceTests
{
    private const int Branch1 = 1, Branch2 = 2, ManagerId = 2, HrId = 4;
    private static readonly DateTime Now = new(2038, 6, 10, 10, 0, 0);

    private readonly DatabaseFixture _db;

    public ExportServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => ExportServiceTests.Now;
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private sealed class TestUser : ICurrentUserService
    {
        private readonly string[] _roles;

        public TestUser(int userId, int? branchId, params string[] roles)
        {
            UserId = userId;
            BranchId = branchId;
            _roles = roles.Length == 0 ? new[] { RoleNames.Employee } : roles;
        }

        public int? UserId { get; }
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => _roles.Contains(role);
    }

    private static readonly TestUser Manager1 = new(ManagerId, Branch1, RoleNames.Manager);
    private static readonly TestUser Hr = new(HrId, null, RoleNames.Hr);
    private static readonly TestUser Staff = new(3, Branch1);

    private static ExportService Export(DbSession s, ICurrentUserService user) => new(new TimesheetRepository(s), new PayrollPeriodRepository(s), new PayslipRepository(s),
        new BranchRepository(s), new ExcelExporter(), user, new FixedClock(), NullLogger<ExportService>.Instance);

    private static XLWorkbook Open(ExportFile file) => new(new MemoryStream(file.Content));

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId = Branch1)
    {
        var code = $"X{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Nhân viên {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static Task SheetAsync(DbSession s, User user, int month, decimal days) => new TimesheetRepository(s).UpsertDraftAsync(new MonthlyTimesheet
    {
        UserId = user.Id, BranchId = user.BranchId, Year = 2038, Month = month, StandardWorkDays = 26, ScheduledShifts = 20, WorkedShifts = 20, ActualWorkDays = days,
        ActualWorkHours = days * 8, CalculatedAt = Now
    });

    /// <summary>Mã nhân viên ở cột B của sheet "Bảng công" (bỏ dòng tiêu đề và dòng tổng).</summary>
    private static List<string> Codes(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheet("Bảng công");
        var codes = new List<string>();
        for (var row = 5; !sheet.Cell(row, 1).GetString().StartsWith("Tổng", StringComparison.Ordinal) && row < 5000; row++) codes.Add(sheet.Cell(row, 2).GetString());
        return codes;
    }

    // ------------------------------------------------------------------ Bảng công

    [SkippableFact]
    public async Task Timesheet_export_follows_the_role_and_branch_scope()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var inBranch1 = await NewEmployeeAsync(s);
        var inBranch2 = await NewEmployeeAsync(s, Branch2);
        await SheetAsync(s, inBranch1, 3, 20);
        await SheetAsync(s, inBranch2, 3, 10);

        // HR: tất cả chi nhánh
        var all = await Export(s, Hr).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 3 });
        Assert.Equal(("bang-cong-2038-03.xlsx", ExportFile.XlsxContentType), (all.FileName, all.ContentType));
        using (var workbook = Open(all))
        {
            Assert.Equal(new[] { inBranch1.EmployeeCode, inBranch2.EmployeeCode }.Order(), Codes(workbook).Order());
            Assert.Contains("Tất cả chi nhánh", workbook.Worksheet("Bảng công").Cell(2, 1).GetString());
        }

        // HR chọn một chi nhánh: tên tệp có mã chi nhánh, tiêu đề có tên chi nhánh
        var only2 = await Export(s, Hr).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 3, BranchId = Branch2 });
        Assert.Equal("bang-cong-2038-03-cn2.xlsx", only2.FileName);
        using (var workbook = Open(only2))
        {
            Assert.Equal(inBranch2.EmployeeCode, Assert.Single(Codes(workbook)));
            Assert.DoesNotContain("Tất cả", workbook.Worksheet("Bảng công").Cell(2, 1).GetString());
        }

        // Quản lý chi nhánh 1: mặc định chi nhánh mình; chọn chi nhánh khác bị từ chối
        var manager = await Export(s, Manager1).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 3 });
        Assert.Equal("bang-cong-2038-03-cn1.xlsx", manager.FileName);
        using (var workbook = Open(manager))
        {
            Assert.Contains(inBranch1.EmployeeCode, Codes(workbook));
            Assert.DoesNotContain(inBranch2.EmployeeCode, Codes(workbook));
        }
        await Assert.ThrowsAsync<ForbiddenException>(() => Export(s, Manager1).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 3, BranchId = Branch2 }));
        await Assert.ThrowsAsync<ForbiddenException>(() => Export(s, Staff).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 3 }));

        // Đầu vào
        await Assert.ThrowsAsync<BadRequestException>(() => Export(s, Hr).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 13 }));
        await Assert.ThrowsAsync<BadRequestException>(() => Export(s, Hr).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 1999, Month = 1 }));
        var defaultMonth = await Export(s, Hr).ExportTimesheetsAsync(new ExportTimesheetsRequest()); // mặc định theo đồng hồ: 06/2038
        Assert.Equal("bang-cong-2038-06.xlsx", defaultMonth.FileName);
    }

    [SkippableFact]
    public async Task Timesheet_export_includes_every_row_even_beyond_one_page()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        const int count = 105; // > 100 dòng mỗi trang
        var codes = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var user = await NewEmployeeAsync(s);
            await SheetAsync(s, user, 4, 1 + i % 20);
            codes.Add(user.EmployeeCode);
        }

        using var workbook = Open(await Export(s, Hr).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 4 }));
        var exported = Codes(workbook);
        Assert.Equal(count, exported.Count);
        Assert.Equal(codes.Order(), exported.Order());
        Assert.Contains($"{count} nhân viên", workbook.Worksheet("Bảng công").Cell(2, 1).GetString());
        Assert.StartsWith("SUM(", workbook.Worksheet("Bảng công").Cell(5 + count, 13).FormulaA1);
    }

    [SkippableFact]
    public async Task A_month_without_timesheets_exports_an_empty_workbook()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();

        using var workbook = Open(await Export(s, Hr).ExportTimesheetsAsync(new ExportTimesheetsRequest { Year = 2038, Month = 1 }));
        Assert.Equal("Không có dữ liệu.", workbook.Worksheet("Bảng công").Cell(5, 1).GetString());
    }

    // ------------------------------------------------------------------ Bảng lương

    [SkippableFact]
    public async Task Payroll_export_contains_payslips_and_their_items()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var periods = new PayrollPeriodRepository(s);
        var periodId = await periods.EnsureAsync(2038, 5, HrId);
        Assert.True(await periods.TryUpdateCalculationAsync(periodId, 26, 1.5m, 1, 8_880_000, Now));

        var payslip = new Payslip
        {
            UserId = employee.Id, BranchId = Branch1, EmploymentType = EmploymentType.FullTime, BaseSalary = 10_400_000, HourlyRate = 50000, StandardWorkDays = 26,
            WorkDays = 20, WorkHours = 160, OvertimeHours = 4, BasicPay = 8_000_000, AllowanceTotal = 650_000, OvertimePay = 300_000, PenaltyTotal = 70_000, NetPay = 8_880_000
        };
        var items = new[]
        {
            new PayslipItem { Kind = PayslipItemKind.Basic, Name = "Lương theo công", Quantity = 20, UnitAmount = 400000, Amount = 8_000_000 },
            new PayslipItem { Kind = PayslipItemKind.Penalty, Name = "Phạt đi muộn / về sớm", Quantity = 2, UnitAmount = 35000, Amount = -70_000, Note = "Đi muộn 30 phút" }
        };
        await new PayslipRepository(s).ReplaceForPeriodAsync(periodId, new[] { new PayslipWithItems(payslip, items) });

        var file = await Export(s, Hr).ExportPayrollAsync(periodId);
        Assert.Equal(("bang-luong-2038-05.xlsx", ExportFile.XlsxContentType), (file.FileName, file.ContentType));
        using var workbook = Open(file);

        var main = workbook.Worksheet("Bảng lương");
        Assert.Contains("BẢN NHÁP", main.Cell(2, 1).GetString()); // kỳ chưa chốt
        Assert.Equal((employee.EmployeeCode, employee.FullName, 8_000_000d, 8_880_000d),
            (main.Cell(5, 2).GetString(), main.Cell(5, 3).GetString(), main.Cell(5, 13).GetDouble(), main.Cell(5, 17).GetDouble()));
        Assert.Equal("Chưa gửi", main.Cell(5, 18).GetString());
        Assert.Equal(8_880_000d, main.Cell(6, 17).GetDouble()); // dòng tổng

        var details = workbook.Worksheet("Chi tiết khoản");
        Assert.Equal(new[] { "Lương theo công", "Phạt đi muộn / về sớm" }, new[] { 5, 6 }.Select(r => details.Cell(r, 4).GetString()));
        Assert.Equal(-70_000d, details.Cell(6, 7).GetDouble());
        Assert.Equal("Đi muộn 30 phút", details.Cell(6, 8).GetString());

        Assert.Equal((employee.EmployeeCode, employee.FullName, 8_880_000d),
            (main.Cell(5, 2).GetString(), main.Cell(5, 3).GetString(), workbook.Worksheet("Theo chi nhánh").Cell(5, 7).GetDouble()));

        // Chốt kỳ → bản chính thức (không còn nhãn nháp)
        Assert.True(await periods.TryFinalizeAsync(periodId, HrId, Now));
        using var finalized = Open(await Export(s, Hr).ExportPayrollAsync(periodId));
        Assert.DoesNotContain("BẢN NHÁP", finalized.Worksheet("Bảng lương").Cell(2, 1).GetString());
        Assert.Contains("Đã chốt", finalized.Worksheet("Bảng lương").Cell(2, 1).GetString());
    }

    [SkippableFact]
    public async Task Payroll_export_rejects_an_unknown_period()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();

        await Assert.ThrowsAsync<NotFoundException>(() => Export(s, Hr).ExportPayrollAsync(99_999_999));
    }
}
