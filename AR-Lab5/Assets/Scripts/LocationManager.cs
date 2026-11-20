using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocationManager : MonoBehaviour
{
    [Header("Источники координат")]
    [SerializeField] private LocationProviderBase editorProvider; // кнопки (эмуляция)
    [SerializeField] private LocationProviderBase deviceProvider; // GPS
    [SerializeField] private bool useMockProvider = false;        // стартовать сразу в режиме кнопок
    [SerializeField] private bool fallbackToMock = true;          // если GPS не запустился, переходим на кнопки

    [Header("Настройки")]
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private float visibilityRadius = 200f;
    [SerializeField] private int nearestCount = 5;
    [SerializeField] private float exitHysteresis = 1.15f;
    [SerializeField] private int smoothingSamples = 3;
    [SerializeField] private float defaultPoiRadius = 20f;

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
    public bool IsMock => provider is MockLocationProvider;
    public string SourceInfo => provider == null
        ? "no provider"
        : $"{provider.GetType().Name} ({provider.Status})";

    private LocationProviderBase provider;
    private IPoiRepository repository;
    private List<Poi> pois = new List<Poi>();
    private readonly Queue<(double lat, double lon)> samples = new Queue<(double, double)>();
    private bool hasPosition;          // хоть раз получали позицию
    private Coroutine initRoutine;

    private void Awake()
    {
        repository = new JsonPoiRepository();
        pois = repository.Load();
        provider = (Application.isEditor || useMockProvider) ? editorProvider : deviceProvider;
    }

    private void Start()
    {
        if (provider == null)
        {
            Debug.LogError("LocationManager: не назначен провайдер координат.");
            return;
        }
        StartCoroutine(PollLoop());
        initRoutine = StartCoroutine(InitProvider(provider));
    }

    private IEnumerator PollLoop()
    {
        var wait = new WaitForSeconds(updateInterval);
        while (true)
        {
            if (IsReady && provider != null && provider.IsReady) Tick();
            yield return wait;
        }
    }

    private IEnumerator InitProvider(LocationProviderBase target)
    {
        IsReady = false;
        yield return target.Init();

        if (provider != target) yield break; // за это время переключили источник

        if (target.IsReady)
        {
            IsReady = true;
            yield break;
        }

        if (fallbackToMock && target != editorProvider && editorProvider != null)
        {
            Debug.LogWarning("GPS недоступен, переключаюсь на кнопки.");
            ActivateMock();
        }
        else
        {
            Debug.LogError("LocationManager: источник координат не готов.");
        }
    }

    // ---------- Переключение источника ----------

    public void ToggleMockMode() => SetMockMode(!IsMock);

    public void SetMockMode(bool mock)
    {
        if (mock == IsMock) return;

        if (mock)
        {
            if (editorProvider == null) return;
            StopInit();
            ActivateMock();
        }
        else
        {
            if (deviceProvider == null) return;
            StopInit();
            samples.Clear();
            provider = deviceProvider;
            initRoutine = StartCoroutine(InitProvider(provider));
        }
    }

    private void StopInit()
    {
        if (initRoutine != null) StopCoroutine(initRoutine);
        initRoutine = null;
    }

    private void ActivateMock()
    {
        provider = editorProvider;
        samples.Clear();
        if (provider is MockLocationProvider m)
        {
            if (hasPosition) m.SetPosition(CurrentLat, CurrentLon); // с последней известной точки
            else m.ResetToStart();
        }
        IsReady = provider.IsReady;
    }

    // ---------- Основной цикл ----------

    private void Tick()
    {
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
        hasPosition = true;
    }

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
            if (d <= ActivePoi.radius * exitHysteresis) return;

            var left = ActivePoi;
            ActivePoi = null;
            ZoneExited?.Invoke(left);
        }

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
        UpdateZone();
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