using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatLoadoutPanel : MonoBehaviour
{
    [SerializeField] private RealTimeCombatLoadout loadout;
    [SerializeField] private CombatAttackLibrary attackLibrary;
    [SerializeField] private TMP_Text[] slotLabels = new TMP_Text[RealTimeCombatLoadout.SlotCount];
    [SerializeField] private TMP_Text[] libraryLabels;
    [SerializeField] private TMP_Text detailsText;

    private int selectedSlot;

    private void OnEnable()
    {
        if (loadout == null)
        {
            loadout = FindAnyObjectByType<RealTimeCombatLoadout>();
        }

        if (loadout != null) loadout.LoadoutChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (loadout != null) loadout.LoadoutChanged -= Refresh;
    }

    public void SelectSlot(int slotIndex)
    {
        if (loadout == null || slotIndex < 0 || slotIndex >= RealTimeCombatLoadout.SlotCount)
        {
            return;
        }

        selectedSlot = slotIndex;
        CombatAttackDefinition attack = loadout.GetAttack(selectedSlot);
        if (detailsText != null)
        {
            detailsText.text = attack == null
                ? string.Empty
                : attack.DisplayName + "\n" + attack.Description + "\nLumiere: " + attack.LightDamage + " | Clarite: " + attack.ClarityGain;
        }
    }

    public void EquipLibraryAttack(int libraryIndex)
    {
        if (loadout == null || attackLibrary == null)
        {
            return;
        }

        loadout.SetAttack(selectedSlot, attackLibrary.GetAttack(libraryIndex));
        SelectSlot(selectedSlot);
    }

    private void Refresh()
    {
        for (int i = 0; i < slotLabels.Length; i++)
        {
            if (slotLabels[i] == null) continue;
            CombatAttackDefinition attack = loadout != null ? loadout.GetAttack(i) : null;
            slotLabels[i].text = attack == null ? "-" : attack.DisplayName;
        }

        for (int i = 0; i < libraryLabels.Length; i++)
        {
            if (libraryLabels[i] == null) continue;
            CombatAttackDefinition attack = attackLibrary != null ? attackLibrary.GetAttack(i) : null;
            libraryLabels[i].text = attack == null ? string.Empty : attack.DisplayName;
        }
    }
}
