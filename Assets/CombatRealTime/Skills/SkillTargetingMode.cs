using UnityEngine;

public enum SkillTargetingMode
{
    [InspectorName("Non configure")] Unconfigured = -1,
    [InspectorName("Directionnel libre")] FreeAttack = 0,
    [InspectorName("Ennemi facultatif")] OptionalTarget = 1,
    [InspectorName("Ennemi obligatoire")] RequiredTarget = 2,
    [InspectorName("Zone autour du lanceur")] CasterArea = 3,
    [InspectorName("Allie selectionne")] SelectedAlly = 4,
    [InspectorName("Soi-meme")] Self = 5,
    [InspectorName("Zone alliee")] AllyArea = 6,
    [InspectorName("Ennemi verrouille obligatoire")] LockedTarget = 7
}
