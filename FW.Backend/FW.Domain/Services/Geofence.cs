namespace FW.Domain.Services;

/// <summary>Kết quả kiểm tra lớp 1 (GPS Geofence).</summary>
/// <param name="DistanceMeters">Khoảng cách từ vị trí điện thoại tới tâm chi nhánh (m).</param>
/// <param name="RadiusMeters">Bán kính chấm công hợp lệ của chi nhánh (m).</param>
public record GeofenceResult(double DistanceMeters, int RadiusMeters)
{
    public bool IsInside => DistanceMeters <= RadiusMeters;
}

/// <summary>
/// Lớp 1 của chấm công: khoảng cách đường tròn lớn (công thức Haversine) giữa vị trí điện thoại và tâm chi nhánh,
/// so với bán kính Geofence. Sai số của Haversine (coi Trái Đất là hình cầu) dưới 0,5% — ở bán kính vài chục
/// tới vài trăm mét chỉ lệch dưới 1 m, nhỏ hơn nhiều so với sai số GPS của điện thoại.
/// </summary>
public static class Geofence
{
    /// <summary>Bán kính trung bình của Trái Đất (m) — giá trị IUGG.</summary>
    public const double EarthRadiusMeters = 6_371_008.8;

    /// <summary>Tọa độ hợp lệ: vĩ độ [-90, 90], kinh độ [-180, 180], không phải NaN/vô cực.</summary>
    public static bool IsValidCoordinate(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    /// <summary>
    /// Khoảng cách Haversine (m):
    /// a = sin²(Δφ/2) + cos φ1 · cos φ2 · sin²(Δλ/2);  d = 2R · atan2(√a, √(1−a)).
    /// </summary>
    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        if (!IsValidCoordinate(lat1, lng1) || !IsValidCoordinate(lat2, lng2))
        {
            throw new ArgumentOutOfRangeException(nameof(lat1), "Tọa độ không hợp lệ.");
        }

        var phi1 = ToRadians(lat1);
        var phi2 = ToRadians(lat2);
        var dPhi = ToRadians(lat2 - lat1);
        var dLambda = ToRadians(lng2 - lng1);

        var sinPhi = Math.Sin(dPhi / 2);
        var sinLambda = Math.Sin(dLambda / 2);
        var a = sinPhi * sinPhi + Math.Cos(phi1) * Math.Cos(phi2) * sinLambda * sinLambda;
        a = Math.Clamp(a, 0, 1); // chặn sai số làm tròn ở hai điểm gần đối xứng tâm

        return 2 * EarthRadiusMeters * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>Kiểm tra vị trí điện thoại có nằm trong vùng chấm công của chi nhánh không.</summary>
    public static GeofenceResult Evaluate(double centerLat, double centerLng, int radiusMeters, double latitude, double longitude) =>
        new(DistanceMeters(centerLat, centerLng, latitude, longitude), radiusMeters);

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
