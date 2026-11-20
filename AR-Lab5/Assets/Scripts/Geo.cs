using System;

public static class Geo
{
    private const double EarthRadius = 6371000.0; // м
    private const double DegToRad = Math.PI / 180.0;

    // Формула Haversine: расстояние по дуге большого круга, метры
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * DegToRad;
        double dLon = (lon2 - lon1) * DegToRad;

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * DegToRad) * Math.Cos(lat2 * DegToRad) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * EarthRadius * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }
}