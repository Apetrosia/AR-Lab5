using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class GpsLocationProvider : LocationProviderBase
{
    [SerializeField] private float desiredAccuracy = 10f;
    [SerializeField] private float updateDistance = 1f;
    [SerializeField] private float initTimeout = 45f;   // холодный старт GPS может быть долгим

    private bool ready;
    private string status = "not started";

    public override bool IsReady => ready && Input.location.status == LocationServiceStatus.Running;
    public override double Latitude => Input.location.lastData.latitude;
    public override double Longitude => Input.location.lastData.longitude;
    public override float Accuracy => Input.location.lastData.horizontalAccuracy;

    public override string Status
    {
        get
        {
            if (Input.location.status != LocationServiceStatus.Running)
                return $"{status}, service {Input.location.status}";

            var d = Input.location.lastData;
            double now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            return $"{status}, acc {d.horizontalAccuracy:F0} m, fix age {now - d.timestamp:F0} s";
        }
    }

    public override IEnumerator Init()
    {
#if UNITY_ANDROID
        if (!HasLocationPermission())
        {
            status = "requesting permission";
            bool answered = false;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => answered = true;

            // Android 12+: оба разрешения одним запросом
            Permission.RequestUserPermissions(
                new[] { Permission.FineLocation, Permission.CoarseLocation }, callbacks);

            float t0 = Time.realtimeSinceStartup;
            while (!answered && Time.realtimeSinceStartup - t0 < 60f)
                yield return null;
            yield return null;

            if (!HasLocationPermission())
            {
                status = "permission denied";
                Debug.LogError("Нет разрешения на геолокацию.");
                yield break;
            }
        }
#endif
        if (!Input.location.isEnabledByUser)
        {
            status = "location services OFF";
            Debug.LogError("Геолокация выключена в системе.");
            yield break;
        }

        status = "starting";
        Input.location.Start(desiredAccuracy, updateDistance);

        float start = Time.realtimeSinceStartup;
        while (Input.location.status == LocationServiceStatus.Initializing
               && Time.realtimeSinceStartup - start < initTimeout)
            yield return null;

        if (Input.location.status != LocationServiceStatus.Running)
        {
            status = "start failed";
            Debug.LogError("GPS не запустился, статус: " + Input.location.status);
            yield break;
        }

        status = "ok";
        ready = true;
    }

#if UNITY_ANDROID
    private static bool HasLocationPermission() =>
        Permission.HasUserAuthorizedPermission(Permission.FineLocation) ||
        Permission.HasUserAuthorizedPermission(Permission.CoarseLocation);
#endif

    private void OnDestroy()
    {
        if (ready) Input.location.Stop();
    }
}