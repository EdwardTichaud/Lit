using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewZone", menuName = "Symphonie/Zone")]
public class ZoneSO : ScriptableObject
{
    [Header("Nom unique de la zone")]
    public string zoneName;
    public string description;

    [Header("Battlefields de cette zone")]
    public List<GameObject> battlefields = new List<GameObject>();

    [Header("Musics")]
    public AudioClipSO zoneMusic;
    public AudioClipSO[] battleMusic;

    [Header("Harmonique prédominante")]
    [Tooltip("Harmonique mise en avant lorsque les musiques de cette zone sont jouées.")]
    public HarmonicType predominantHarmonic = HarmonicType.Lumiere;
}
