using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class GpsLocationProvider : LocationProviderBase
{
    [SerializeField] private float desiredAccuracy = 1f;
    [SerializeField] private float updateDistance = 1f;
    [SerializeField] private float initTimeout = 20f;

    private bool ready;

    public override bool IsReady => ready && Input.location.status == LocationServiceStatus.Running;
    public override double Latitude => Input.location.lastData.latitude;
    public override double Longitude => Input.location.lastData.longitude;
    public override float Accuracy => Input.location.lastData.horizontalAccuracy;

    public override IEnumerator Init()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            float t0 = Time.realtimeSinceStartup;
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation)
                   && Time.realtimeSinceStartup - t0 < 30f)
                yield return null;
        }
#endif
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("Геолокация выключена или нет разрешения.");
            yield break;
        }

        Input.location.Start(desiredAccuracy, updateDistance);

        float start = Time.realtimeSinceStartup;
        while (Input.location.status == LocationServiceStatus.Initializing
               && Time.realtimeSinceStartup - start < initTimeout)
            yield return null;

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogError("GPS не запустился, статус: " + Input.location.status);
            yield break;
        }

        ready = true;
    }

    private void OnDestroy()
    {
        if (ready) Input.location.Stop();
    }
}