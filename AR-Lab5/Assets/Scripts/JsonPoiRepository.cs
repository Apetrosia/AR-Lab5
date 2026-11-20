using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class JsonPoiRepository : IPoiRepository
{
    private readonly string path = Path.Combine(Application.persistentDataPath, "pois.json");

    public string Info => path;

    public List<Poi> Load()
    {
        if (!File.Exists(path)) return new List<Poi>();
        try
        {
            var list = JsonUtility.FromJson<PoiList>(File.ReadAllText(path));
            return list?.items ?? new List<Poi>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Не удалось прочитать {path}: {e.Message}");
            return new List<Poi>();
        }
    }

    public void Save(List<Poi> pois)
    {
        File.WriteAllText(path, JsonUtility.ToJson(new PoiList { items = pois }, true));
    }
}