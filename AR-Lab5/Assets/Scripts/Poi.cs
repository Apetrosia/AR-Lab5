using System;
using System.Collections.Generic;

[Serializable]
public class Poi
{
    public string id;
    public string name;
    public double lat;
    public double lon;
    public float radius;      // метры
    public string contentId;  // ключ привязанного контента (пригодится в части 2)
}

[Serializable]
public class PoiList
{
    public List<Poi> items = new List<Poi>();
}

public readonly struct PoiDistance
{
    public readonly Poi poi;
    public readonly double distance; // метры

    public PoiDistance(Poi poi, double distance)
    {
        this.poi = poi;
        this.distance = distance;
    }
}