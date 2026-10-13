using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private SpiritBondController combatSpiritBond;

    private void RefreshCombatSpiritFusion(bool active)
    {
        if (!active || playerRoot == null || !playerRoot.gameObject.activeInHierarchy || IsPlayerDead())
        {
            if (combatSpiritBond != null) combatSpiritBond.SetCombatFusion(false);
            combatSpiritBond = null;
            return;
        }

        var bond = SpiritBondController.FindForCharacter(playerRoot.gameObject);
        // Playable Munin was retired from the prefabs; restore only the bond's
        // presentation, never a playable companion or an invisible squad actor.
        if (bond == null) bond = playerRoot.gameObject.AddComponent<SpiritBondController>();
        if (!bond.isActiveAndEnabled)
        {
            RefreshCombatSpiritFusion(false);
            return;
        }
        if (combatSpiritBond != bond)
        {
            if (combatSpiritBond != null) combatSpiritBond.SetCombatFusion(false);
            combatSpiritBond = bond;
        }
        combatSpiritBond.SetCombatFusion(true);
    }

    private void OnDisable() => RefreshCombatSpiritFusion(false);
}
