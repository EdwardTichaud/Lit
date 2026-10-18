using UnityEngine;

/// <summary>Dedicated attack volume, unrelated to the actor's body collider.</summary>
[DisallowMultipleComponent]
public sealed class CombatLabClashActor : MonoBehaviour
{
    public CombatLabClashController controller;
    public Transform volumeAnchor;
    public Vector3 localCenter = new Vector3(0, 1.1f, .8f);
    [Min(.01f)] public float radius = .55f;
    public int Generation => GetComponent<LitBrainsEnemy>() is LitBrainsEnemy enemy
        ? enemy.ActionSequenceId : GetComponent<PlayerActionPresentationController>() is PlayerActionPresentationController presentation
            ? presentation.ActionGeneration : -1;
    public Vector3 Center => (volumeAnchor != null ? volumeAnchor : transform).TransformPoint(localCenter);
    public bool Open { get; private set; }
    public int WindowGeneration { get; private set; } = -1;
    public Vector3 PreviousCenter { get; private set; }
    public int OpenedFrame { get; private set; }

    public void SetWindow(AnimationEvent source, bool open)
    {
        if (controller == null) return;
        bool accepted = controller.Accept(this, source);
        if (controller.diagnostics) Debug.Log($"[CombatClash] Window actor={name} open={open} accepted={accepted} generation={Generation} clip={source?.animatorClipInfo.clip?.name}", this);
        if (!accepted) return;
        if (open)
        {
            if (Open && WindowGeneration == Generation) return;
            WindowGeneration = Generation;
            PreviousCenter = Center;
            OpenedFrame = Time.frameCount;
        }
        Open = open;
        // A closing event must not erase a contact earlier in this Animator frame.
        controller.RecordWindow(this, open);
    }
    public void Sample() { PreviousCenter = Center; }
    public void ResetWindow() { Open = false; WindowGeneration = -1; }
    private void OnDisable() { ResetWindow(); if (controller != null) controller.ActorDisabled(this); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Open ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(Center, radius);
    }
}
