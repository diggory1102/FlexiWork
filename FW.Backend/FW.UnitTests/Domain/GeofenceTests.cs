using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class GeofenceTests
{
    // Một độ trên kinh tuyến (cung tròn lớn) = π·R/180
    private const double MetersPerDegree = Math.PI * Geofence.EarthRadiusMeters / 180;

    [Fact]
    public void Same_point_is_zero_meters() =>
        Assert.Equal(0, Geofence.DistanceMeters(21.0285, 105.8542, 21.0285, 105.8542), precision: 9);

    [Fact]
    public void One_degree_of_latitude_matches_great_circle_arc() =>
        Assert.Equal(MetersPerDegree, Geofence.DistanceMeters(21, 105.8, 22, 105.8), precision: 6);

    [Fact]
    public void Longitude_degrees_shrink_by_cosine_of_latitude()
    {
        // Trên xích đạo 1 độ kinh = 1 độ vĩ; ở vĩ độ 60° khoảng cách ngắn lại ~ một nửa
        Assert.Equal(MetersPerDegree, Geofence.DistanceMeters(0, 100, 0, 101), precision: 6);
        var atSixty = Geofence.DistanceMeters(60, 100, 60, 101);
        Assert.InRange(atSixty, MetersPerDegree * 0.49, MetersPerDegree * 0.51);
    }

    [Fact]
    public void Distance_is_symmetric_and_handles_antimeridian()
    {
        var ab = Geofence.DistanceMeters(21.0285, 105.8542, 10.7769, 106.7009);
        var ba = Geofence.DistanceMeters(10.7769, 106.7009, 21.0285, 105.8542);
        Assert.Equal(ab, ba, precision: 6);

        // 179,5°E ↔ 179,5°W chỉ cách nhau 1 độ kinh qua kinh tuyến 180, không phải 359 độ
        Assert.Equal(MetersPerDegree, Geofence.DistanceMeters(0, 179.5, 0, -179.5), precision: 6);
    }

    [Fact]
    public void Antipodal_points_are_half_the_circumference() =>
        Assert.Equal(Math.PI * Geofence.EarthRadiusMeters, Geofence.DistanceMeters(0, 0, 0, 180), precision: 3);

    [Fact]
    public void Hanoi_to_Ho_Chi_Minh_City_is_about_1140_km()
    {
        // Hồ Gươm (Hà Nội) ↔ Nhà thờ Đức Bà (TP.HCM): khoảng cách đường chim bay ≈ 1.140 km
        var meters = Geofence.DistanceMeters(21.0285, 105.8542, 10.7798, 106.6990);
        Assert.InRange(meters, 1_135_000, 1_145_000);
    }

    [Theory]
    [InlineData(0.0003, true)]   // ≈ 33 m về phía bắc → trong bán kính 50 m
    [InlineData(0.00044, true)]  // ≈ 48,9 m
    [InlineData(0.00046, false)] // ≈ 51,2 m
    [InlineData(0.01, false)]    // ≈ 1,1 km
    public void Evaluate_compares_distance_with_branch_radius(double latOffset, bool inside)
    {
        var result = Geofence.Evaluate(21.0285, 105.8542, 50, 21.0285 + latOffset, 105.8542);
        Assert.Equal(inside, result.IsInside);
        Assert.Equal(50, result.RadiusMeters);
        Assert.Equal(latOffset * MetersPerDegree, result.DistanceMeters, precision: 3);
    }

    [Fact]
    public void Point_exactly_on_the_boundary_is_inside() =>
        Assert.True(new GeofenceResult(50, 50).IsInside);

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-90.0001, 0)]
    [InlineData(0, 180.5)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Invalid_coordinates_are_rejected(double lat, double lng)
    {
        Assert.False(Geofence.IsValidCoordinate(lat, lng));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geofence.DistanceMeters(21, 105, lat, lng));
    }
}
