
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    /// <summary>Giới hạn số tin nhắn của một người mỗi ngày (giờ VN) để kiểm soát tải/chi phí.</summary>
    public int MaxMessagesPerUserPerDay { get; set; } = 60;

    public int MaxMessageLength { get; set; } = 1000;

    /// <summary>Số vòng "mô hình gọi công cụ → nhận kết quả" tối đa cho một câu hỏi.</summary>
    public int MaxToolIterations { get; set; } = 5;

    /// <summary>Số tin gần nhất của hội thoại được nạp lại làm ngữ cảnh.</summary>
    public int HistoryMessages { get; set; } = 12;

    /// <summary>Bản nháp hết hạn sau số phút này nếu chưa xác nhận.</summary>
    public int DraftTtlMinutes { get; set; } = 30;

    /// <summary>Cắt kết quả công cụ dài để khỏi tràn ngữ cảnh mô hình nhỏ.</summary>
    public int MaxToolResultChars { get; set; } = 6000;

    /// <summary>Số ngày tối đa một đơn nghỉ được trải dài.</summary>
    public int MaxLeaveSpanDays { get; set; } = 31;
}
