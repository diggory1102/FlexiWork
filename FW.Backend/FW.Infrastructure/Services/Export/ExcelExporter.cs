using ClosedXML.Excel;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Exports;
using FW.Domain.Enums;

namespace FW.Infrastructure.Services.Export;

/// <summary>
/// Dựng tệp Excel bằng ClosedXML. Chữ do người dùng nhập (tên, ghi chú) luôn được ghi là văn bản — không bao giờ thành công thức; không dùng
/// <c>AdjustToContents</c> (cần phông chữ hệ thống, Linux trên Render không có) mà đặt sẵn độ rộng cột.
/// </summary>
public class ExcelExporter : IExcelExporter
{
    private const string MoneyFormat = "#,##0";
    private const string QuantityFormat = "#,##0.##";
    private const string DecimalFormat = "0.00";
    private static readonly XLColor HeaderColor = XLColor.FromHtml("#2563EB");
    private static readonly XLColor TotalColor = XLColor.FromHtml("#E2E8F0");

    private sealed record Column(string Title, double Width, string? Format = null, bool Total = false);

    // ------------------------------------------------------------------ Bảng công

    private static readonly Column[] TimesheetColumns =
    {
        new("STT", 6), new("Mã NV", 12), new("Họ tên", 26), new("Chi nhánh", 22), new("Chức vụ", 22),
        new("Ca được xếp", 11, "0", true), new("Ca đã làm", 10, "0", true), new("Ca vắng", 9, "0", true), new("Chờ duyệt", 10, "0", true),
        new("Bị từ chối", 10, "0", true), new("Thiếu check-out", 12, "0", true),
        new("Công chuẩn", 11, DecimalFormat), new("Ngày công thực tế", 13, DecimalFormat, true), new("Giờ làm", 10, DecimalFormat, true),
        new("Phút đi muộn", 11, "0", true), new("Phút về sớm", 11, "0", true), new("Số lần vi phạm", 11, "0", true),
        new("Công bị trừ", 10, DecimalFormat, true), new("Tiền phạt (đ)", 13, MoneyFormat, true), new("Giờ OT", 9, DecimalFormat, true),
        new("Nghỉ có lương (công)", 13, DecimalFormat, true), new("Nghỉ không lương (công)", 14, DecimalFormat, true),
        new("Ca đêm", 8, "0", true), new("Ngày ca gãy", 10, "0", true),
        new("Trạng thái", 14), new("Người xác nhận", 20), new("Người khóa", 20), new("Ghi chú", 28)
    };

    public byte[] BuildTimesheets(TimesheetWorkbook data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Bảng công");
        var firstRow = WriteHeader(sheet, $"BẢNG CÔNG THÁNG {data.Month:00}/{data.Year}",
            $"Phạm vi: {data.ScopeLabel} · {data.Rows.Count} nhân viên · Xuất lúc {data.ExportedAt:dd/MM/yyyy HH:mm}", TimesheetColumns);

        var row = firstRow;
        foreach (var t in data.Rows)
        {
            var f = 1;
            Number(sheet, row, f++, row - firstRow + 1);
            Text(sheet, row, f++, t.EmployeeCode);
            Text(sheet, row, f++, t.FullName);
            Text(sheet, row, f++, t.BranchName);
            Text(sheet, row, f++, t.PositionTitle);
            Number(sheet, row, f++, t.ScheduledShifts);
            Number(sheet, row, f++, t.WorkedShifts);
            Number(sheet, row, f++, t.AbsentShifts);
            Number(sheet, row, f++, t.PendingLogs);
            Number(sheet, row, f++, t.RejectedLogs);
            Number(sheet, row, f++, t.MissingCheckouts);
            Number(sheet, row, f++, t.StandardWorkDays);
            Number(sheet, row, f++, t.ActualWorkDays);
            Number(sheet, row, f++, t.ActualWorkHours);
            Number(sheet, row, f++, t.TotalLateMinutes);
            Number(sheet, row, f++, t.TotalEarlyMinutes);
            Number(sheet, row, f++, t.ViolationCount);
            Number(sheet, row, f++, t.DeductedWorkDays);
            Number(sheet, row, f++, t.PenaltyAmount);
            Number(sheet, row, f++, t.TotalOvertimeHours);
            Number(sheet, row, f++, t.PaidLeaveDays);
            Number(sheet, row, f++, t.UnpaidLeaveDays);
            Number(sheet, row, f++, t.NightShifts);
            Number(sheet, row, f++, t.SplitShiftDays);
            Text(sheet, row, f++, TimesheetStatusLabel(t.Status));
            Text(sheet, row, f++, t.ConfirmedByName);
            Text(sheet, row, f++, t.LockedByName);
            Text(sheet, row, f++, t.Note);
            row++;
        }

        Finish(sheet, TimesheetColumns, firstRow, row - 1, freezeColumns: 3);
        return Save(workbook);
    }

    // ------------------------------------------------------------------ Bảng lương

    private static readonly Column[] PayrollColumns =
    {
        new("STT", 6), new("Mã NV", 12), new("Họ tên", 26), new("Chi nhánh", 22), new("Chức vụ", 22), new("Hình thức", 15),
        new("Lương cơ bản (đ)", 16, MoneyFormat), new("Đơn giá giờ (đ)", 14, MoneyFormat), new("Công chuẩn", 11, DecimalFormat),
        new("Ngày công", 10, DecimalFormat, true), new("Giờ làm", 10, DecimalFormat, true), new("Giờ OT", 9, DecimalFormat, true),
        new("Lương theo công (đ)", 17, MoneyFormat, true), new("Phụ cấp (đ)", 14, MoneyFormat, true), new("Tiền OT (đ)", 14, MoneyFormat, true),
        new("Phạt (đ)", 13, MoneyFormat, true), new("Thực nhận (đ)", 16, MoneyFormat, true), new("Email phiếu lương", 16)
    };

    private static readonly Column[] ItemColumns =
    {
        new("Mã NV", 12), new("Họ tên", 26), new("Loại", 16), new("Khoản", 30), new("Số lượng", 11, QuantityFormat),
        new("Đơn giá (đ)", 15, MoneyFormat), new("Thành tiền (đ)", 16, MoneyFormat, true), new("Ghi chú", 34)
    };

    private static readonly Column[] BranchColumns =
    {
        new("Chi nhánh", 26), new("Số nhân viên", 13, "0", true), new("Lương theo công (đ)", 17, MoneyFormat, true), new("Phụ cấp (đ)", 14, MoneyFormat, true),
        new("Tiền OT (đ)", 14, MoneyFormat, true), new("Phạt (đ)", 13, MoneyFormat, true), new("Thực nhận (đ)", 16, MoneyFormat, true)
    };

    public byte[] BuildPayroll(PayrollWorkbook data)
    {
        var (period, slips) = (data.Period, data.Payslips);
        var draft = period.Status != PayrollPeriodStatus.Finalized;
        var status = draft ? "BẢN NHÁP — kỳ lương chưa chốt, số liệu có thể thay đổi" : $"Đã chốt{(period.FinalizedAt is { } at ? $" lúc {at:dd/MM/yyyy HH:mm}" : string.Empty)}";
        var subtitle = $"{status} · {slips.Count} phiếu lương · Công chuẩn {period.StandardWorkDays:0.##} · Hệ số OT {period.OvertimeMultiplier:0.##} · Xuất lúc {data.ExportedAt:dd/MM/yyyy HH:mm}";

        using var workbook = new XLWorkbook();

        // Sheet 1: mỗi nhân viên một dòng
        var sheet = workbook.AddWorksheet("Bảng lương");
        var first = WriteHeader(sheet, $"BẢNG LƯƠNG THÁNG {period.Month:00}/{period.Year}", subtitle, PayrollColumns);
        var row = first;
        foreach (var p in slips.OrderBy(p => p.BranchName).ThenBy(p => p.EmployeeCode))
        {
            var f = 1;
            Number(sheet, row, f++, row - first + 1);
            Text(sheet, row, f++, p.EmployeeCode);
            Text(sheet, row, f++, p.FullName);
            Text(sheet, row, f++, p.BranchName);
            Text(sheet, row, f++, p.PositionTitle);
            Text(sheet, row, f++, p.EmploymentType == EmploymentType.PartTime ? "Bán thời gian" : "Toàn thời gian");
            Number(sheet, row, f++, p.BaseSalary);
            Number(sheet, row, f++, p.HourlyRate);
            Number(sheet, row, f++, p.StandardWorkDays);
            Number(sheet, row, f++, p.WorkDays);
            Number(sheet, row, f++, p.WorkHours);
            Number(sheet, row, f++, p.OvertimeHours);
            Number(sheet, row, f++, p.BasicPay);
            Number(sheet, row, f++, p.AllowanceTotal);
            Number(sheet, row, f++, p.OvertimePay);
            Number(sheet, row, f++, p.PenaltyTotal);
            Number(sheet, row, f++, p.NetPay);
            Text(sheet, row, f++, p.Status == PayslipStatus.Sent ? "Đã gửi" : "Chưa gửi");
            row++;
        }
        Finish(sheet, PayrollColumns, first, row - 1, freezeColumns: 3);

        // Sheet 2: từng khoản trên phiếu lương
        var details = workbook.AddWorksheet("Chi tiết khoản");
        var detailFirst = WriteHeader(details, $"CHI TIẾT PHIẾU LƯƠNG THÁNG {period.Month:00}/{period.Year}", subtitle, ItemColumns);
        row = detailFirst;
        foreach (var p in slips.OrderBy(p => p.EmployeeCode))
        {
            foreach (var item in data.Items.GetValueOrDefault(p.Id) ?? Array.Empty<Domain.Entities.PayslipItem>())
            {
                var f = 1;
                Text(details, row, f++, p.EmployeeCode);
                Text(details, row, f++, p.FullName);
                Text(details, row, f++, ItemKindLabel(item.Kind));
                Text(details, row, f++, item.Name);
                Number(details, row, f++, item.Quantity);
                Number(details, row, f++, item.UnitAmount);
                Number(details, row, f++, item.Amount);
                Text(details, row, f++, item.Note);
                row++;
            }
        }
        Finish(details, ItemColumns, detailFirst, row - 1, freezeColumns: 2);

        // Sheet 3: tổng hợp theo chi nhánh
        var branches = workbook.AddWorksheet("Theo chi nhánh");
        var branchFirst = WriteHeader(branches, $"TỔNG HỢP THEO CHI NHÁNH — THÁNG {period.Month:00}/{period.Year}", subtitle, BranchColumns);
        row = branchFirst;
        foreach (var g in slips.GroupBy(p => p.BranchName ?? "Chưa gán chi nhánh").OrderBy(g => g.Key))
        {
            var f = 1;
            Text(branches, row, f++, g.Key);
            Number(branches, row, f++, g.Count());
            Number(branches, row, f++, g.Sum(p => p.BasicPay));
            Number(branches, row, f++, g.Sum(p => p.AllowanceTotal));
            Number(branches, row, f++, g.Sum(p => p.OvertimePay));
            Number(branches, row, f++, g.Sum(p => p.PenaltyTotal));
            Number(branches, row, f++, g.Sum(p => p.NetPay));
            row++;
        }
        Finish(branches, BranchColumns, branchFirst, row - 1, freezeColumns: 1);

        return Save(workbook);
    }

    // ------------------------------------------------------------------ Dùng chung

    /// <summary>Tiêu đề, dòng phụ và hàng tiêu đề cột. Trả về số dòng dữ liệu đầu tiên.</summary>
    private static int WriteHeader(IXLWorksheet sheet, string title, string subtitle, IReadOnlyList<Column> columns)
    {
        const int headerRow = 4;
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = subtitle;
        sheet.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#64748B"));

        for (var i = 0; i < columns.Count; i++)
        {
            var cell = sheet.Cell(headerRow, i + 1);
            cell.Value = columns[i].Title;
            sheet.Column(i + 1).Width = columns[i].Width;
            if (columns[i].Format is { } format) sheet.Column(i + 1).Style.NumberFormat.Format = format;
        }

        var header = sheet.Range(headerRow, 1, headerRow, columns.Count);
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        header.Style.Fill.SetBackgroundColor(HeaderColor);
        header.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText();
        sheet.Row(headerRow).Height = 34;
        return headerRow + 1;
    }

    /// <summary>Dòng tổng (công thức SUM), viền, bộ lọc và cố định hàng / cột tiêu đề.</summary>
    private static void Finish(IXLWorksheet sheet, IReadOnlyList<Column> columns, int firstDataRow, int lastDataRow, int freezeColumns)
    {
        var headerRow = firstDataRow - 1;
        sheet.SheetView.FreezeRows(headerRow);
        sheet.SheetView.FreezeColumns(freezeColumns);

        if (lastDataRow >= firstDataRow)
        {
            sheet.Range(headerRow, 1, lastDataRow, columns.Count).SetAutoFilter();
            var totalRow = lastDataRow + 1;
            sheet.Cell(totalRow, 1).Value = "Tổng cộng";
            for (var i = 0; i < columns.Count; i++)
            {
                if (!columns[i].Total) continue;
                var letter = XLHelper.GetColumnLetterFromNumber(i + 1);
                sheet.Cell(totalRow, i + 1).FormulaA1 = $"SUM({letter}{firstDataRow}:{letter}{lastDataRow})";
            }
            var total = sheet.Range(totalRow, 1, totalRow, columns.Count);
            total.Style.Font.SetBold();
            total.Style.Fill.SetBackgroundColor(TotalColor);
            sheet.Range(headerRow, 1, totalRow, columns.Count).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }
        else
        {
            sheet.Cell(firstDataRow, 1).Value = "Không có dữ liệu.";
            sheet.Cell(firstDataRow, 1).Style.Font.SetItalic();
        }
    }

    /// <summary>Ghi chữ dạng văn bản: một giá trị như "=1+1" hay "@SUM(...)" vẫn chỉ là chữ, không được Excel tính như công thức.</summary>
    private static void Text(IXLWorksheet sheet, int row, int column, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var cell = sheet.Cell(row, column);
        cell.SetValue(value);
        cell.Style.NumberFormat.Format = "@";
    }

    private static void Number(IXLWorksheet sheet, int row, int column, decimal value) => sheet.Cell(row, column).Value = value;

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string TimesheetStatusLabel(string status) => status switch
    {
        TimesheetStatus.Draft => "Nháp",
        TimesheetStatus.Confirmed => "Đã xác nhận",
        TimesheetStatus.Locked => "Đã khóa",
        TimesheetStatus.Finalized => "Đã chốt lương",
        _ => status
    };

    private static string ItemKindLabel(string kind) => kind switch
    {
        PayslipItemKind.Basic => "Lương",
        PayslipItemKind.Allowance => "Phụ cấp",
        PayslipItemKind.Overtime => "Làm thêm giờ",
        PayslipItemKind.Penalty => "Phạt",
        PayslipItemKind.Adjustment => "Điều chỉnh",
        _ => kind
    };
}
