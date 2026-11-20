using System.Collections.Generic;

public interface IPoiRepository
{
    string Info { get; }          // где лежат данные (для отладки)
    List<Poi> Load();
    void Save(List<Poi> pois);
}