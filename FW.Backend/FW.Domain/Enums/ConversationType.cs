namespace FW.Domain.Enums;

/// <summary>Loại phòng chat (cột conversations.type).</summary>
public static class ConversationType
{
    /// <summary>Chat trực tiếp giữa hai người.</summary>
    public const string Direct = "direct";

    /// <summary>Nhóm chat — hiện là nhóm tự động của từng chi nhánh (UC 7.3); nhóm tùy chỉnh (UC 7.2) là phần tùy chọn.</summary>
    public const string Group = "group";

    public static readonly IReadOnlyList<string> All = new[] { Direct, Group };
}
