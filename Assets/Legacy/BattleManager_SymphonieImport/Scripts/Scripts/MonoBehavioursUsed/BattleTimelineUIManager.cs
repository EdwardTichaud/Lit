using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Gère l'interface de la timeline de combat affichant l'ordre des tours.
/// Ce gestionnaire instancie et organise les <see cref="BattleTimelineUnit"/> sous
/// un conteneur placé dans la scène de bataille.
/// </summary>
public class BattleTimelineUIManager : MonoBehaviour
{
    /// <summary>
    /// Instance unique accessible globalement.
    /// </summary>
    public static BattleTimelineUIManager Instance { get; private set; }

    [Header("Références UI")]
    [SerializeField] private RectTransform timelineContainer; // Parent accueillant les vignettes, enfant de BattleScene
    [SerializeField] private GameObject timelineUnitPrefab;    // Préfabriqué de vignette

    /// <summary>
    /// Liste des vignettes actuellement affichées dans la timeline.
    /// </summary>
    private readonly List<BattleTimelineUnit> timelineUIObjects = new();

    /// <summary>
    /// Accès au conteneur pour d'éventuels réglages extérieurs.
    /// </summary>
    public RectTransform TimelineContainer => timelineContainer;

    /// <summary>
    /// Objet racine de la timeline (parent du conteneur) utilisé pour
    /// afficher ou masquer facilement l'interface complète.
    /// </summary>
    private GameObject TimelineRoot => timelineContainer != null
        ? timelineContainer.parent?.parent?.gameObject
        : null;

    private void Awake()
    {
        // Mise en place du singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Instancie les vignettes représentant les unités actives.
    /// </summary>
    public void Initialize(IEnumerable<CharacterUnit> characters)
    {
        Clear();

        foreach (var unit in characters)
        {
            var slot = Instantiate(timelineUnitPrefab, timelineContainer);
            var ui = slot.GetComponent<BattleTimelineUnit>();
            ui.Initialize(unit);
            timelineUIObjects.Add(ui);
        }

        // Dès l'initialisation, on tente de trier la liste selon l'ordre
        // de passage prévu afin d'éviter un affichage incohérent si des
        // valeurs d'ATB étaient déjà présentes (cas de reprises ou tests).
        SortTimelineByATB();
    }

    public void AddToTimeline(CharacterUnit unit)
    {
        if (unit == null || timelineContainer == null || timelineUnitPrefab == null)
            return;

        if (timelineUIObjects.Any(x => x != null && x.BoundUnit == unit))
            return;

        var slot = Instantiate(timelineUnitPrefab, timelineContainer);
        var ui = slot.GetComponent<BattleTimelineUnit>();
        if (ui == null)
        {
            Destroy(slot);
            return;
        }

        ui.Initialize(unit);
        timelineUIObjects.Add(ui);
        SortTimelineByATB();
    }

    /// <summary>
    /// Active ou désactive l'affichage de la timeline complète.
    /// </summary>
    /// <param name="visible">true pour afficher, false pour masquer.</param>
    public void SetVisible(bool visible)
    {
        // On récupère l'objet racine (parent du parent) afin d'éviter
        // que d'autres scripts doivent connaître la hiérarchie précise.
        GameObject root = TimelineRoot;
        if (root != null)
            root.SetActive(visible);
    }

    /// <summary>
    /// Efface toutes les vignettes existantes.
    /// </summary>
    public void Clear()
    {
        foreach (var ui in timelineUIObjects)
            if (ui != null) Destroy(ui.gameObject);
        timelineUIObjects.Clear();
    }

    /// <summary>
    /// Calcule le nombre d'itérations nécessaires pour qu'une unité soit prête
    /// à jouer en reproduisant la logique de <see cref="NewBattleManager"/>.
    /// </summary>
    private float EstimateTurnsToReady(CharacterUnit unit)
    {
        if (unit == null || unit.currentInitiative <= 0f)
            return float.MaxValue;

        // Reste d'ATB à parcourir avant d'atteindre le seuil de déclenchement.
        float remaining = Mathf.Max(0f, unit.ATBMax - unit.currentATB);
        return remaining / unit.currentInitiative;
    }

    /// <summary>
    /// Trie les vignettes de la timeline selon l'ordre réel de passage calculé
    /// à partir des jauges d'ATB et de l'initiative de chaque unité.
    /// </summary>
    private void SortTimelineByATB()
    {
        timelineUIObjects.Sort((a, b) =>
        {
            // Accès direct aux CharacterUnit liés aux vignettes pour éviter
            // tout couplage supplémentaire avec le NewBattleManager.
            float turnsA = EstimateTurnsToReady(a.BoundUnit);
            float turnsB = EstimateTurnsToReady(b.BoundUnit);
            return turnsA.CompareTo(turnsB);
        });
    }

    /// <summary>
    /// Met en évidence l'unité actuellement active.
    /// </summary>
    private void UpdateHighlight(CharacterUnit activeUnit)
    {
        foreach (var ui in timelineUIObjects)
        {
            // Compare directement les CharacterUnit pour simplifier la logique
            // et éviter les recherches inutiles dans les collections du manager.
            bool isCurrent = activeUnit != null && ui.BoundUnit == activeUnit;
            ui.SetHighlight(isCurrent);
        }
    }

    /// <summary>
    /// Réorganise la timeline pour placer l'unité active en première position et
    /// afficher ensuite les suivantes selon l'ordre réel de passage.
    /// </summary>
    private void UpdateWheel(CharacterUnit activeUnit)
    {
        if (timelineUIObjects.Count == 0)
            return;

        // Trie préalablement la liste pour refléter l'ordre de passage calculé.
        SortTimelineByATB();

        if (activeUnit != null)
        {
            // S'assure que l'unité actuellement active est bien en tête.
            int index = timelineUIObjects.FindIndex(ui => ui.BoundUnit == activeUnit);
            if (index > 0)
            {
                var ui = timelineUIObjects[index];
                timelineUIObjects.RemoveAt(index);
                timelineUIObjects.Insert(0, ui);
            }
        }

        // Met à jour l'apparence et l'ordre des vignettes dans la hiérarchie.
        int count = timelineUIObjects.Count;
        for (int i = 0; i < count; i++)
        {
            timelineUIObjects[i].transform.SetSiblingIndex(i);

            float t = count > 1 ? Mathf.Clamp01((float)i / (count - 1)) : 0f;
            float scale = Mathf.Lerp(1f, 0.8f, t);
            float alpha = Mathf.Lerp(1f, 0.2f, t);
            timelineUIObjects[i].SetAppearance(scale, alpha);
            // Met à jour la jauge d'ATB pour refléter l'avancée réelle.
            timelineUIObjects[i].UpdateATBGauge();
        }
    }

    /// <summary>
    /// Point d'entrée unique pour mettre à jour l'ordre et la mise en
    /// évidence de la timeline. Les scripts externes n'ont plus à se
    /// soucier de l'ordre des appels.
    /// </summary>
    /// <param name="activeUnit">Unité actuellement active (peut être nulle).</param>
    public void Refresh(CharacterUnit activeUnit)
    {
        UpdateHighlight(activeUnit);
        UpdateWheel(activeUnit);
    }

    /// <summary>
    /// Supprime de la timeline l'unité donnée.
    /// </summary>
    public void RemoveFromTimeline(CharacterUnit deadUnit)
    {
        // Recherche directe sur l'unité liée : plus robuste si plusieurs
        // personnages partagent des données communes.
        var ui = timelineUIObjects.FirstOrDefault(x => x.BoundUnit == deadUnit);
        if (ui != null)
        {
            timelineUIObjects.Remove(ui);
            Destroy(ui.gameObject);
        }
    }
}
