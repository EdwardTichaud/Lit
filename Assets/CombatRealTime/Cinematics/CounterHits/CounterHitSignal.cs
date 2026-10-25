using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>One authored counter contact, independent of the victim's rig or animation clips.</summary>
[DisplayName("CombatLab_CounterHit_Damage_Animation_VFX_SFX")]
[CustomStyle("SignalEmitter")]
public sealed class CounterHitSignal : Marker, INotification, INotificationOptionProvider
{
    [Min(0), Tooltip("Degats de cet impact. Arrondis vers les points de vie entiers lors de l'application.")]
    public float damage;
    [Min(0), Tooltip("0 : aucune animation. Sinon etat de la victime : Hit_1, Hit_2, Hit_3...")]
    public int hitIndex = 1;
    [Range(0, .25f)] public float blendSeconds = .04f;
    public GameObject vfx;
    public AudioClipSO sfx;
    public PropertyName id => new PropertyName(nameof(CounterHitSignal));
    public NotificationFlags flags => NotificationFlags.TriggerOnce;
    public bool IsValid => !float.IsNaN(damage) && !float.IsInfinity(damage) && damage >= 0 && hitIndex >= 0 &&
        !float.IsNaN(blendSeconds) && !float.IsInfinity(blendSeconds) && blendSeconds >= 0 && blendSeconds <= .25f;
    public static int RoundDamage(float value) => float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? 0 :
        (int)System.Math.Min(int.MaxValue, System.Math.Round((double)value));
}
