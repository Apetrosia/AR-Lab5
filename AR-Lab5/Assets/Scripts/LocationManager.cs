using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocationManager : MonoBehaviour
{
    [Header("Источники координат")]
    [SerializeField] private LocationProviderBase editorProvider; // в редакторе
    [SerializeField] private LocationProviderBase deviceProvider; // на телефоне

    [Header("Настройки")]
    [SerializeField] private float updateInterval = 0.5f;     // период опроса, с
    [SerializeField] private float visibilityRadius = 200f;   // радиус видимости, м
    [SerializeField] private int nearestCount = 5;            // сколько ближайших показывать
    [SerializeField] private float exitHysteresis = 1.15f;    // выход при dist > radius * k
    [SerializeField] private int smoothingSamples = 3;        // сглаживание GPS (скользящее среднее)
    [SerializeField] private float defaultPoiRadius = 20f;    // радиус новой метки, м

    public event Action<Poi> ZoneEntered;
    public event Action<Poi> ZoneExited;
    public event Action<IReadOnlyList<PoiDistance>> NearestUpdated;
    public event Action<Poi> PoiAdded;

    public bool IsReady { get; private set; }
    public double CurrentLat { get; private set; }
    public double CurrentLon { get; private set; }
    public float Accuracy { get; private set; }
    public Poi ActivePoi { get; private set; }
    public IReadOnlyList<Poi> Pois => pois;
    public string StoragePath => repository.Info;

    private LocationProviderBase provider;
    private IPoiRepository repository;
    private List<Poi> pois = new List<Poi>();
    private readonly Queue<(double lat, double lon)> samples = new Queue<(double, double)>();

    private void Awake()
    {
        repository = new JsonPoiRepository();
        pois = repository.Load();
        provider = Application.isEditor ? editorProvider : deviceProvider;
    }

    private IEnumerator Start()
    {
        if (provider == null)
        {
            Debug.LogError("LocationManager: не назначен провайдер координат.");
            yield break;
        }

        yield return provider.Init();

        if (!provider.IsReady)
        {
            Debug.LogError("LocationManager: источник координат не готов.");
            yield break;
        }

        IsReady = true;
        var wait = new WaitForSeconds(updateInterval);
        while (true)
        {
            Tick();
            yield return wait;
        }
    }

    private void Tick()
    {
        if (!provider.IsReady) return;
        UpdatePosition();
        NearestUpdated?.Invoke(GetNearest(nearestCount, visibilityRadius));
        UpdateZone();
    }

    private void UpdatePosition()
    {
        samples.Enqueue((provider.Latitude, provider.Longitude));
        while (samples.Count > Mathf.Max(1, smoothingSamples)) samples.Dequeue();

        double sumLat = 0, sumLon = 0;
        foreach (var s in samples) { sumLat += s.lat; sumLon += s.lon; }
        CurrentLat = sumLat / samples.Count;
        CurrentLon = sumLon / samples.Count;
        Accuracy = provider.Accuracy;
    }

    // N ближайших меток в пределах maxDistance, отсортированных по расстоянию
    public List<PoiDistance> GetNearest(int n, double maxDistance)
    {
        var result = new List<PoiDistance>();
        foreach (var p in pois)
        {
            double d = Geo.Distance(CurrentLat, CurrentLon, p.lat, p.lon);
            if (d <= maxDistance) result.Add(new PoiDistance(p, d));
        }
        result.Sort((a, b) => a.distance.CompareTo(b.distance));
        if (result.Count > n) result.RemoveRange(n, result.Count - n);
        return result;
    }

    private void UpdateZone()
    {
        if (ActivePoi != null)
        {
            double d = Geo.Distance(CurrentLat, CurrentLon, ActivePoi.lat, ActivePoi.lon);
            if (d <= ActivePoi.radius * exitHysteresis) return; // всё ещё в зоне

            var left = ActivePoi;
            ActivePoi = null;
            ZoneExited?.Invoke(left);
        }

        // Ищем ближайшую зону, в которую попали
        Poi best = null;
        double bestDist = double.MaxValue;
        foreach (var p in pois)
        {
            double d = Geo.Distance(CurrentLat, CurrentLon, p.lat, p.lon);
            if (d <= p.radius && d < bestDist) { best = p; bestDist = d; }
        }

        if (best != null)
        {
            ActivePoi = best;
            ZoneEntered?.Invoke(best);
        }
    }

    // Кнопка «поставить метку здесь»
    public Poi AddPoiHere(string poiName = null, float radius = -1f, string contentId = "default")
    {
        if (!IsReady)
        {
            Debug.LogWarning("Позиция ещё не определена.");
            return null;
        }

        var poi = new Poi
        {
            id = Guid.NewGuid().ToString(),
            name = string.IsNullOrWhiteSpace(poiName) ? $"POI {pois.Count + 1}" : poiName,
            lat = CurrentLat,
            lon = CurrentLon,
            radius = radius > 0 ? radius : defaultPoiRadius,
            contentId = contentId
        };

        pois.Add(poi);
        repository.Save(pois);
        PoiAdded?.Invoke(poi);
        UpdateZone(); // сразу попадаем в только что созданную зону
        return poi;
    }

    public void ClearAll()
    {
        if (ActivePoi != null)
        {
            var left = ActivePoi;
            ActivePoi = null;
            ZoneExited?.Invoke(left);
        }
        pois.Clear();
        repository.Save(pois);
    }
}