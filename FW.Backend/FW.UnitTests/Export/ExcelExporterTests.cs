using ClosedXML.Excel;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Exports;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Services.Export;

namespace FW.UnitTests.Export;

public class ExcelExporterTests
{
    private static readonly DateTime ExportedAt = new(2026, 11, 17, 9, 30, 0);
    private readonly ExcelExporter _exporter = new();

    private static XLWorkbook Open(byte[] content) => new(new MemoryStream(content));

    private static TimesheetRow Sheet(string code, string name, string? branch = "FlexiWork Cầu Giấy", decimal days = 20, decimal penalty = 70000, string status = TimesheetStatus.Locked) => new()
    {
        Id = 1, UserId = 1, EmployeeCode = code, FullName = name, BranchName = branch, PositionTitle = "Nhân viên Bán hàng", Month = 10, Year = 2026,
        StandardWorkDays = 26, ScheduledShifts = 22, WorkedShifts = 20, AbsentShifts = 1, PendingLogs = 0, ActualWorkDays = days, ActualWorkHours = days * 8,
        TotalLateMinutes = 30, TotalEarlyMinutes = 10, ViolationCount = 2, DeductedWorkDays = 0.5m, PenaltyAmount = penalty, TotalOvertimeHours = 4.5m,
        PaidLeaveDays = 1, UnpaidLeaveDays = 0, NightShifts = 3, SplitShiftDays = 2, Status = status, ConfirmedByName = "Quản lý A", LockedByName = "HR B", Note = "Ghi chú"
    };

    private static TimesheetWorkbook Timesheets(params TimesheetRow[] rows) => new(2026, 10, "Tất cả chi nhánh", ExportedAt, rows);

    // ------------------------------------------------------------------ Bảng công

    [Fact]
    public void Timesheet_workbook_has_title_headers_rows_and_formula_totals()
    {
        using var workbook = Open(_exporter.BuildTimesheets(Timesheets(Sheet("NV001", "Nguyễn Văn A"), Sheet("NV002", "Trần Thị B", days: 10, penalty: 0))));
        var sheet = workbook.Worksheet("Bảng công");

        Assert.Equal("BẢNG CÔNG THÁNG 10/2026", sheet.Cell(1, 1).GetString());
        Assert.Contains("Tất cả chi nhánh", sheet.Cell(2, 1).GetString());
        Assert.Contains("2 nhân viên", sheet.Cell(2, 1).GetString());
        Assert.Contains("17/11/2026 09:30", sheet.Cell(2, 1).GetString());

        Assert.Equal("Mã NV", sheet.Cell(4, 2).GetString());
        Assert.Equal("Ngày công thực tế", sheet.Cell(4, 13).GetString());

        Assert.Equal(("NV001", "Nguyễn Văn A", "FlexiWork Cầu Giấy", "Nhân viên Bán hàng"),
            (sheet.Cell(5, 2).GetString(), sheet.Cell(5, 3).GetString(), sheet.Cell(5, 4).GetString(), sheet.Cell(5, 5).GetString()));
        Assert.Equal((1d, 2d), (sheet.Cell(5, 1).GetDouble(), sheet.Cell(6, 1).GetDouble()));        // STT
        Assert.Equal((22d, 20d, 1d), (sheet.Cell(5, 6).GetDouble(), sheet.Cell(5, 7).GetDouble(), sheet.Cell(5, 8).GetDouble()));
        Assert.Equal((20d, 10d), (sheet.Cell(5, 13).GetDouble(), sheet.Cell(6, 13).GetDouble()));
        Assert.Equal(70000d, sheet.Cell(5, 19).GetDouble());
        Assert.Equal("Đã khóa", sheet.Cell(5, 25).GetString());
        Assert.Equal(("Quản lý A", "HR B"), (sheet.Cell(5, 26).GetString(), sheet.Cell(5, 27).GetString()));

        // Dòng tổng dùng công thức SUM nên khớp khi người dùng sửa số liệu
        Assert.Equal("Tổng cộng", sheet.Cell(7, 1).GetString());
        Assert.True(sheet.Cell(7, 13).HasFormula);
        Assert.Equal("SUM(M5:M6)", sheet.Cell(7, 13).FormulaA1);
        Assert.Equal(30d, sheet.Cell(7, 13).GetDouble());
        Assert.Equal(70000d, sheet.Cell(7, 19).GetDouble());
        Assert.False(sheet.Cell(7, 12).HasFormula); // cột công chuẩn không cộng tổng

        Assert.Equal(4, sheet.SheetView.SplitRow);   // cố định tiêu đề
        Assert.Equal(3, sheet.SheetView.SplitColumn); // cố định STT / mã / tên
        Assert.True(sheet.AutoFilter.IsEnabled);
    }

    [Fact]
    public void Timesheet_workbook_without_data_says_so_and_has_no_totals()
    {
        using var workbook = Open(_exporter.BuildTimesheets(Timesheets()));
        var sheet = workbook.Worksheet("Bảng công");

        Assert.Equal("Không có dữ liệu.", sheet.Cell(5, 1).GetString());
        Assert.True(sheet.Cell(5, 13).IsEmpty());
        Assert.False(sheet.AutoFilter.IsEnabled);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil.test\",\"Bấm vào đây\")")]
    [InlineData("=1+1")]
    [InlineData("+cmd|' /C calc'!A0")]
    [InlineData("@SUM(1+1)")]
    [InlineData("-2+3")]
    public void User_text_is_never_written_as_a_formula(string malicious)
    {
        var row = Sheet("NV001", malicious);
        row.Note = malicious;
        row.PositionTitle = malicious;
        using var workbook = Open(_exporter.BuildTimesheets(Timesheets(row)));
        var sheet = workbook.Worksheet("Bảng công");

        foreach (var column in new[] { 3, 5, 28 })
        {
            Assert.False(sheet.Cell(5, column).HasFormula);
            Assert.Equal(XLDataType.Text, sheet.Cell(5, column).DataType);
            Assert.Equal(malicious, sheet.Cell(5, column).GetString());
        }
    }

    [Fact]
    public void Missing_optional_text_leaves_cells_empty()
    {
        var row = Sheet("NV001", "Nguyễn Văn A", branch: null);
        row.PositionTitle = null;
        row.ConfirmedByName = null;
        row.Note = null;
        using var workbook = Open(_exporter.BuildTimesheets(Timesheets(row)));
        var sheet = workbook.Worksheet("Bảng công");

        Assert.True(sheet.Cell(5, 4).IsEmpty());
        Assert.True(sheet.Cell(5, 5).IsEmpty());
        Assert.True(sheet.Cell(5, 26).IsEmpty());
        Assert.True(sheet.Cell(5, 28).IsEmpty());
    }

    [Theory]
    [InlineData(TimesheetStatus.Draft, "Nháp")]
    [InlineData(TimesheetStatus.Confirmed, "Đã xác nhận")]
    [InlineData(TimesheetStatus.Locked, "Đã khóa")]
    [InlineData(TimesheetStatus.Finalized, "Đã chốt lương")]
    public void Timesheet_status_is_shown_in_vietnamese(string status, string label)
    {
        using var workbook = Open(_exporter.BuildTimesheets(Timesheets(Sheet("NV001", "A", status: status))));
        Assert.Equal(label, workbook.Worksheet("Bảng công").Cell(5, 25).GetString());
    }

    // ------------------------------------------------------------------ Bảng lương

    private static PayrollPeriodRow Period(string status = PayrollPeriodStatus.Finalized) => new()
    {
        Id = 1, Month = 10, Year = 2026, Status = status, StandardWorkDays = 26, OvertimeMultiplier = 1.5m, FinalizedAt = new DateTime(2026, 11, 3, 14, 0, 0)
    };

    private static PayslipRow Slip(long id, string code, string name, string? branch, decimal basic, decimal allowance, decimal ot, decimal penalty,
        string employment = EmploymentType.FullTime, string status = PayslipStatus.Draft) => new()
    {
        Id = id, UserId = (int)id, EmployeeCode = code, FullName = name, BranchName = branch, PositionTitle = "Nhân viên", EmploymentType = employment,
        BaseSalary = 10_400_000, HourlyRate = 35000, StandardWorkDays = 26, WorkDays = 20, WorkHours = 160, OvertimeHours = 4,
        BasicPay = basic, AllowanceTotal = allowance, OvertimePay = ot, PenaltyTotal = penalty, NetPay = basic + allowance + ot - penalty, Status = status
    };

    private static PayrollWorkbook Payroll(PayrollPeriodRow period, params (PayslipRow Slip, PayslipItem[] Items)[] data) => new(period, ExportedAt,
        data.Select(d => d.Slip).ToList(), data.ToDictionary(d => d.Slip.Id, d => (IReadOnlyList<PayslipItem>)d.Items));

    [Fact]
    public void Payroll_workbook_has_three_sheets_with_rows_details_and_branch_totals()
    {
        var a = Slip(1, "NV001", "Nguyễn Văn A", "FlexiWork Cầu Giấy", 8_000_000, 650_000, 300_000, 70_000, status: PayslipStatus.Sent);
        var b = Slip(2, "NV002", "Trần Thị B", "FlexiWork Cầu Giấy", 2_800_000, 0, 0, 0, EmploymentType.PartTime);
        var c = Slip(3, "NV003", "Lê Văn C", "FlexiWork Hoàn Kiếm", 5_000_000, 100_000, 0, 20_000);
        var data = Payroll(Period(),
            (a, new[]
            {
                new PayslipItem { PayslipId = 1, Kind = PayslipItemKind.Basic, Name = "Lương theo công", Quantity = 20, UnitAmount = 400000, Amount = 8_000_000 },
                new PayslipItem { PayslipId = 1, Kind = PayslipItemKind.Penalty, Name = "Phạt đi muộn / về sớm", Quantity = 2, UnitAmount = 35000, Amount = -70_000, Note = "Đi muộn 30 phút" }
            }),
            (b, Array.Empty<PayslipItem>()),
            (c, new[] { new PayslipItem { PayslipId = 3, Kind = PayslipItemKind.Allowance, Name = "Phụ cấp ăn trưa", Quantity = 1, UnitAmount = 100000, Amount = 100_000 } }));

        using var workbook = Open(_exporter.BuildPayroll(data));
        Assert.Equal(new[] { "Bảng lương", "Chi tiết khoản", "Theo chi nhánh" }, workbook.Worksheets.Select(w => w.Name));

        // Bảng lương: mỗi nhân viên một dòng (xếp theo chi nhánh rồi mã)
        var main = workbook.Worksheet("Bảng lương");
        Assert.Equal("BẢNG LƯƠNG THÁNG 10/2026", main.Cell(1, 1).GetString());
        Assert.Contains("Đã chốt lúc 03/11/2026 14:00", main.Cell(2, 1).GetString());
        Assert.DoesNotContain("BẢN NHÁP", main.Cell(2, 1).GetString());
        Assert.Equal(new[] { "NV001", "NV002", "NV003" }, new[] { 5, 6, 7 }.Select(r => main.Cell(r, 2).GetString()));
        Assert.Equal(("Toàn thời gian", "Bán thời gian"), (main.Cell(5, 6).GetString(), main.Cell(6, 6).GetString()));
        Assert.Equal((8_000_000d, 650_000d, 300_000d, 70_000d, 8_880_000d),
            (main.Cell(5, 13).GetDouble(), main.Cell(5, 14).GetDouble(), main.Cell(5, 15).GetDouble(), main.Cell(5, 16).GetDouble(), main.Cell(5, 17).GetDouble()));
        Assert.Equal(("Đã gửi", "Chưa gửi"), (main.Cell(5, 18).GetString(), main.Cell(6, 18).GetString()));
        Assert.Equal("Tổng cộng", main.Cell(8, 1).GetString());
        Assert.Equal("SUM(Q5:Q7)", main.Cell(8, 17).FormulaA1);
        Assert.Equal(8_880_000d + 2_800_000d + 5_080_000d, main.Cell(8, 17).GetDouble());

        // Chi tiết khoản: khoản trừ là số âm
        var details = workbook.Worksheet("Chi tiết khoản");
        Assert.Equal(("NV001", "Lương", "Lương theo công", 8_000_000d), (details.Cell(5, 1).GetString(), details.Cell(5, 3).GetString(), details.Cell(5, 4).GetString(), details.Cell(5, 7).GetDouble()));
        Assert.Equal(("Phạt", -70_000d, "Đi muộn 30 phút"), (details.Cell(6, 3).GetString(), details.Cell(6, 7).GetDouble(), details.Cell(6, 8).GetString()));
        Assert.Equal(("NV003", "Phụ cấp"), (details.Cell(7, 1).GetString(), details.Cell(7, 3).GetString()));
        Assert.Equal(8_000_000d - 70_000d + 100_000d, details.Cell(8, 7).GetDouble());

        // Theo chi nhánh
        var branches = workbook.Worksheet("Theo chi nhánh");
        Assert.Equal(("FlexiWork Cầu Giấy", 2d, 10_800_000d, 8_880_000d + 2_800_000d), (branches.Cell(5, 1).GetString(), branches.Cell(5, 2).GetDouble(), branches.Cell(5, 3).GetDouble(), branches.Cell(5, 7).GetDouble()));
        Assert.Equal(("FlexiWork Hoàn Kiếm", 5_080_000d), (branches.Cell(6, 1).GetString(), branches.Cell(6, 7).GetDouble()));
        Assert.Equal(3d, branches.Cell(7, 2).GetDouble()); // tổng số nhân viên
    }

    [Fact]
    public void A_draft_period_is_marked_as_a_draft_copy()
    {
        using var workbook = Open(_exporter.BuildPayroll(Payroll(Period(PayrollPeriodStatus.Draft), (Slip(1, "NV001", "A", "CN", 1_000_000, 0, 0, 0), Array.Empty<PayslipItem>()))));

        Assert.Contains("BẢN NHÁP", workbook.Worksheet("Bảng lương").Cell(2, 1).GetString());
    }

    [Fact]
    public void Payroll_workbook_without_payslips_is_still_valid()
    {
        using var workbook = Open(_exporter.BuildPayroll(Payroll(Period())));

        Assert.Equal(3, workbook.Worksheets.Count);
        Assert.Equal("Không có dữ liệu.", workbook.Worksheet("Bảng lương").Cell(5, 1).GetString());
        Assert.Equal("Không có dữ liệu.", workbook.Worksheet("Chi tiết khoản").Cell(5, 1).GetString());
    }

    [Fact]
    public void Payroll_user_text_is_never_written_as_a_formula()
    {
        var slip = Slip(1, "NV001", "=cmd|' /C calc'!A0", "=1+1", 1_000_000, 0, 0, 0);
        var item = new PayslipItem { PayslipId = 1, Kind = PayslipItemKind.Allowance, Name = "=HYPERLINK(\"x\")", Quantity = 1, UnitAmount = 1, Amount = 1, Note = "@A1" };
        using var workbook = Open(_exporter.BuildPayroll(Payroll(Period(), (slip, new[] { item }))));

        Assert.False(workbook.Worksheet("Bảng lương").Cell(5, 3).HasFormula);
        Assert.False(workbook.Worksheet("Bảng lương").Cell(5, 4).HasFormula);
        Assert.False(workbook.Worksheet("Chi tiết khoản").Cell(5, 4).HasFormula);
        Assert.False(workbook.Worksheet("Chi tiết khoản").Cell(5, 8).HasFormula);
        Assert.Equal("=1+1", workbook.Worksheet("Theo chi nhánh").Cell(5, 1).GetString());
        Assert.False(workbook.Worksheet("Theo chi nhánh").Cell(5, 1).HasFormula);
    }

    [Fact]
    public void Output_is_a_valid_xlsx_package()
    {
        var bytes = _exporter.BuildTimesheets(Timesheets(Sheet("NV001", "A")));

        Assert.Equal(new byte[] { 0x50, 0x4B }, bytes.Take(2).ToArray()); // "PK" — xlsx là tệp zip
        Assert.True(bytes.Length > 1000);
    }
}
