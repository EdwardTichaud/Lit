using TMPro;
using UnityEngine;

/// <summary>Local-only explanation of a shared maintenance passage; never grants discovery or traversal.</summary>
public sealed class AbilityShortcutSign : MonoBehaviour
{
    public TrouEtroit passage;
    public TMP_Text[] signs = System.Array.Empty<TMP_Text>();
    private float nextRefresh;
    private void LateUpdate()
    {
        var player = LocalPlayerUtils.GetControlledCharacter();
        var camera = Camera.main;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .1f;
        var abilities = AbilityService.Instance;
        foreach (var sign in signs)
        {
            if (sign == null) continue;
            bool visible = player != null && passage != null && Vector3.Distance(sign.transform.position, player.transform.position) < 6f;
            sign.gameObject.SetActive(visible);
            if (!visible) continue;
            sign.text = !passage.IsDetected ? "Jointure de maintenance\n<size=75%>Observateur permet de l'examiner</size>" :
                abilities == null || !abilities.IsUnlocked(passage.saufConduitAbility) ? "Passage étroit\n<size=75%>Sauf-Conduit requis — à débloquer</size>" :
                !abilities.HasEquipped(player, passage.saufConduitAbility) ? "Passage découvert\n<size=75%>Équiper Sauf-Conduit à la Maison</size>" :
                passage.IsInsideInteractionVolume(player.transform.position) ? "Passage de maintenance\n<size=75%>Interagir : traverser</size>" :
                "Passage de maintenance\n<size=75%>Approchez-vous pour traverser</size>";
            if (camera != null) sign.transform.rotation = Quaternion.LookRotation(sign.transform.position - camera.transform.position);
        }
    }
}
