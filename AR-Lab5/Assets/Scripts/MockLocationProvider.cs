using System;
using System.Collections;
using UnityEngine;

public class MockLocationProvider : LocationProviderBase
{
    [SerializeField] private double startLatitude = 55.7558;
    [SerializeField] private double startLongitude = 37.6173;
    [SerializeField] private float accuracy = 5f;

    private double lat, lon;
    private bool ready;

    public override bool IsReady => ready;
    public override double Latitude => lat;
    public override double Longitude => lon;
    public override float Accuracy => accuracy;
    public override string Status => "buttons";

    public override IEnumerator Init()
    {
        if (!ready) ResetToStart();
        yield break;
    }

    public void ResetToStart()
    {
        lat = startLatitude;
        lon = startLongitude;
        ready = true;
    }

    // Поставить «пользователя» в заданную точку (например, последнюю позицию GPS)
    public void SetPosition(double newLat, double newLon)
    {
        lat = newLat;
        lon = newLon;
        ready = true;
    }

    // Сдвиг в метрах: north > 0 — на север, east > 0 — на восток
    public void MoveMeters(double north, double east)
    {
        const double metersPerDegLat = 111194.9;
        lat += north / metersPerDegLat;
        lon += east / (metersPerDegLat * Math.Cos(lat * Math.PI / 180.0));
    }
}