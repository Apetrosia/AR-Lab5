using System.Collections;
using UnityEngine;

public abstract class LocationProviderBase : MonoBehaviour
{
    public abstract bool IsReady { get; }
    public abstract double Latitude { get; }
    public abstract double Longitude { get; }
    public abstract float Accuracy { get; }   // горизонтальная точность, м
    public abstract IEnumerator Init();
}