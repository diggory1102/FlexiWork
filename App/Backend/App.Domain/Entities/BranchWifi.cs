namespace App.Domain.Entities
{
    /// <summary>
    /// BSSID / MAC Address của mạng Wi-Fi tại chi nhánh
    /// </summary>
    public class BranchWifi : BaseEntity
    {
        public int BranchId { get; set; }
        public string Ssid { get; set; } = string.Empty;
        public string Bssid { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // Navigation Property
        public Branch Branch { get; set; } = null!;
    }
}
