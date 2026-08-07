using UnityEngine;

/// <summary>
/// Attach to any future spirit-made weapon (shield, spear, etc.). Its active
/// state automatically hides the free-roaming companion visual.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpiritWeaponManifestation : MonoBehaviour
{
    [SerializeField] private SpiritBondController bond;

    private void OnEnable()
    {
        ResolveBond();
        bond?.RegisterWeaponManifestation(this);
    }

    private void OnDisable()
    {
        bond?.UnregisterWeaponManifestation(this);
    }

    private void ResolveBond()
    {
        if (bond != null)
        {
            return;
        }

        bond = GetComponentInParent<SpiritBondController>();
        if (bond != null)
        {
            return;
        }

        var character = GetComponentInParent<SquadCharacterController>();
        if (character != null)
        {
            bond = SpiritBondController.FindForCharacter(character.gameObject);
        }
    }
}
