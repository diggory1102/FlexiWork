using System;

namespace App.Application.Services
{
    public static class GeofenceCalculator
    {
        private const double EarthRadiusKm = 6371.0;

        /// <summary>
        /// Tính khoảng cách (mét) giữa 2 tọa độ GPS theo công thức Haversine
        /// </summary>
        public static double CalculateDistanceInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distanceInKm = EarthRadiusKm * c;

            return distanceInKm * 1000.0; // Quy đổi ra mét
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }
    }
}
