using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocationDebugUI : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private LocationManager locationManager;
    [SerializeField] private MockLocationProvider mock;
    [SerializeField] private float mockStepMeters = 10f;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text nearestText;
    [SerializeField] private TMP_Text logText;

    [Header("Buttons")]
    [SerializeField] private Button sourceToggleButton;
    [SerializeField] private TMP_Text sourceToggleLabel;
    [SerializeField] private Button addPoiButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private GameObject mockControls;
    [SerializeField] private Button northButton;
    [SerializeField] private Button southButton;
    [SerializeField] private Button westButton;
    [SerializeField] private Button eastButton;

    private readonly Queue<string> log = new Queue<string>();
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private void Awake()
    {
        sourceToggleButton.onClick.AddListener(locationManager.ToggleMockMode);
        addPoiButton.onClick.AddListener(() => locationManager.AddPoiHere());
        clearButton.onClick.AddListener(() => locationManager.ClearAll());
        northButton.onClick.AddListener(() => mock.MoveMeters(mockStepMeters, 0));
        southButton.onClick.AddListener(() => mock.MoveMeters(-mockStepMeters, 0));
        westButton.onClick.AddListener(() => mock.MoveMeters(0, -mockStepMeters));
        eastButton.onClick.AddListener(() => mock.MoveMeters(0, mockStepMeters));
        mockControls.SetActive(Application.isEditor && mock != null);
    }

    private void Start()
    {
        Log("Storage: " + locationManager.StoragePath);
    }

    private void OnEnable()
    {
        locationManager.ZoneEntered += OnEntered;
        locationManager.ZoneExited += OnExited;
        locationManager.PoiAdded += OnAdded;
        locationManager.NearestUpdated += OnNearest;
    }

    private void OnDisable()
    {
        locationManager.ZoneEntered -= OnEntered;
        locationManager.ZoneExited -= OnExited;
        locationManager.PoiAdded -= OnAdded;
        locationManager.NearestUpdated -= OnNearest;
    }

    private void OnEntered(Poi p) => Log($"ENTER zone: {p.name}");
    private void OnExited(Poi p) => Log($"EXIT zone: {p.name}");
    private void OnAdded(Poi p) => Log($"ADDED: {p.name} r={p.radius:F0} m");

    private void OnNearest(IReadOnlyList<PoiDistance> list)
    {
        var sb = new StringBuilder("Nearest:\n");
        if (list.Count == 0) sb.Append("  (none in range)");
        for (int i = 0; i < list.Count; i++)
            sb.AppendLine($"{i + 1}. {list[i].poi.name}  {list[i].distance.ToString("F1", Inv)} m  (r={list[i].poi.radius:F0})");
        nearestText.text = sb.ToString();
    }

    private void Update()
    {
        sourceToggleLabel.text = locationManager.IsMock ? "Use GPS" : "Use buttons";
        bool wantMock = mock != null && locationManager.IsMock;
        if (mockControls.activeSelf != wantMock) mockControls.SetActive(wantMock);
        var m = locationManager;
        var sb = new StringBuilder();
        sb.AppendLine($"Ready: {m.IsReady}");
        sb.AppendLine("Source: " + m.SourceInfo);
        sb.AppendLine($"Lat: {m.CurrentLat.ToString("F6", Inv)}");
        sb.AppendLine($"Lon: {m.CurrentLon.ToString("F6", Inv)}");
        sb.AppendLine($"Accuracy: {m.Accuracy.ToString("F1", Inv)} m");
        sb.AppendLine($"POIs saved: {m.Pois.Count}");
        sb.AppendLine(m.ActivePoi != null
            ? $"Active zone: {m.ActivePoi.name} (r={m.ActivePoi.radius:F0} m)"
            : "Active zone: none");
        statusText.text = sb.ToString();
    }

    private void Log(string msg)
    {
        log.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {msg}");
        while (log.Count > 8) log.Dequeue();
        logText.text = string.Join("\n", log);
    }

    // Правый клик по заголовку компонента → Test Haversine
    [ContextMenu("Test Haversine")]
    private void TestHaversine()
    {
        Debug.Log($"1 градус широты: {Geo.Distance(55, 37, 56, 37):F0} m (ожидается ~111195)");
        Debug.Log($"Москва - Санкт-Петербург: {Geo.Distance(55.7558, 37.6173, 59.9343, 30.3351) / 1000:F0} km (ожидается ~634)");
    }
}