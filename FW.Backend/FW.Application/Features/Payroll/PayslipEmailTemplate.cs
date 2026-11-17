using System.Globalization;
using System.Net;
using System.Text;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Payroll;

/// <summary>Email phiếu lương điện tử (UC 8.4): bảng từng khoản + thực nhận. Mọi nội dung do người dùng nhập đều được mã hóa HTML.</summary>
public static class PayslipEmailTemplate
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Subject(int month, int year) => $"[FlexiWork] Phiếu lương tháng {month:00}/{year}";

    public static string Build(PayslipRow payslip, IReadOnlyList<PayslipItem> items)
    {
        var rows = new StringBuilder();
        foreach (var item in items)
        {
            var note = string.IsNullOrWhiteSpace(item.Note) ? string.Empty : $"<div style=\"color:#64748b;font-size:12px\">{WebUtility.HtmlEncode(item.Note)}</div>";
            var color = item.Amount < 0 ? "#dc2626" : "#0f172a";
            rows.Append($"""
                <tr>
                  <td style="padding:8px 12px;border-bottom:1px solid #e2e8f0">{WebUtility.HtmlEncode(item.Name)}{note}</td>
                  <td style="padding:8px 12px;border-bottom:1px solid #e2e8f0;text-align:right;white-space:nowrap">{Number(item.Quantity)} × {Money(item.UnitAmount)}</td>
                  <td style="padding:8px 12px;border-bottom:1px solid #e2e8f0;text-align:right;white-space:nowrap;color:{color}">{Money(item.Amount)}</td>
                </tr>
                """);
        }

        var kind = payslip.EmploymentType == EmploymentType.PartTime ? "Bán thời gian" : "Toàn thời gian";
        return $"""
            <div style="font-family:Arial,sans-serif;max-width:620px;margin:auto;color:#0f172a">
              <h2 style="color:#2563eb;margin-bottom:4px">FlexiWork</h2>
              <h3 style="margin-top:0">Phiếu lương tháng {payslip.Month:00}/{payslip.Year}</h3>
              <p>Xin chào <strong>{WebUtility.HtmlEncode(payslip.FullName)}</strong> ({WebUtility.HtmlEncode(payslip.EmployeeCode)}), đây là phiếu lương của bạn:</p>
              <table style="width:100%;border-collapse:collapse;font-size:14px">
                <tr><td style="padding:4px 12px 4px 0;color:#64748b">Hình thức</td><td>{kind}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#64748b">Chi nhánh</td><td>{WebUtility.HtmlEncode(payslip.BranchName ?? "—")}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#64748b">Ngày công thực tế</td><td>{Number(payslip.WorkDays)} công · {Number(payslip.WorkHours)} giờ · OT {Number(payslip.OvertimeHours)} giờ</td></tr>
              </table>
              <table style="width:100%;border-collapse:collapse;font-size:14px;margin-top:12px">
                <tr style="background:#f1f5f9">
                  <th style="padding:8px 12px;text-align:left">Khoản</th><th style="padding:8px 12px;text-align:right">Số lượng × đơn giá</th><th style="padding:8px 12px;text-align:right">Thành tiền</th>
                </tr>
                {rows}
                <tr>
                  <td colspan="2" style="padding:12px;text-align:right;font-weight:bold">Thực nhận</td>
                  <td style="padding:12px;text-align:right;font-weight:bold;font-size:16px;color:#2563eb;white-space:nowrap">{Money(payslip.NetPay)}</td>
                </tr>
              </table>
              <p style="color:#64748b;font-size:13px">Bạn cũng xem được phiếu lương này trong ứng dụng FlexiWork. Nếu có thắc mắc, vui lòng liên hệ bộ phận Nhân sự. Không chia sẻ email này cho người khác.</p>
            </div>
            """;
    }

    private static string Money(decimal value) => $"{value.ToString("N0", Vi)} đ";

    private static string Number(decimal value) => value.ToString("0.##", Vi);
}
