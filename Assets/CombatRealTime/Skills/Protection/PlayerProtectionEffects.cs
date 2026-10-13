using UnityEngine;

/// <summary>Actor-owned protection resources. Temporary links are independent of concentration.</summary>
[DisallowMultipleComponent]
public sealed class PlayerProtectionEffects : MonoBehaviour
{
    public SquadCharacterController Actor { get; private set; }
    public SquadCharacterController LinkedAlly { get; private set; }
    public SkillSO LinkSkill { get; private set; }
    public SkillSO AuraSkill { get; private set; }
    public float Reserve { get; private set; }
    public float LinkRemaining => Mathf.Max(0, linkUntil - Time.time);
    public bool HasLink => LinkSkill != null && LinkRemaining > 0 && Alive(Actor) &&
        Alive(LinkedAlly) && Vector3.Distance(Actor.transform.position, LinkedAlly.transform.position) <= LinkSkill.protection.maximumLinkDistance;
    public bool HasAura => AuraSkill != null && Alive(Actor);
    private SkillSO JusticeSkill => Actor != null && Actor.CharacterData != null && Actor.CharacterData.combatSkills != null
        ? Actor.CharacterData.combatSkills.Find(s => s != null && s.IsProtectionSkill && s.protection.kind == SkillProtectionKind.JusteRetour)
            ?? CombatSkillKitInput.GetOwnedSkill(Actor, CombatSkillKit.Aurell, 3) : null;
    public bool IsJusticeUser => JusticeSkill != null;
    private float linkUntil;
    private CombatDamageWorldFeedback linkLabel, auraLabel, reserveLabel;
    private float displayedReserve = -1;

    public static bool Alive(SquadCharacterController actor) => actor != null && actor.isActiveAndEnabled &&
        actor.CurrentHp > 0 && actor.CharacterData != null && !actor.CharacterData.isEnemy;
    public static PlayerProtectionEffects Get(SquadCharacterController actor)
    {
        if (actor == null) return null;
        var effects = actor.GetComponent<PlayerProtectionEffects>();
        return effects != null ? effects : actor.gameObject.AddComponent<PlayerProtectionEffects>();
    }
    private void Awake() => Actor = GetComponent<SquadCharacterController>();

    public bool BeginLink(SkillSO skill, SquadCharacterController ally)
    {
        if (!Alive(Actor) || !Alive(ally) || skill == null || skill.protection.kind != SkillProtectionKind.Intercession ||
            Vector3.Distance(Actor.transform.position, ally.transform.position) > skill.protection.radius) return false;
        ClearLink();
        LinkSkill = skill; LinkedAlly = ally;
        linkUntil = Time.time + skill.protection.durationSeconds;
        linkLabel = CombatDamageWorldFeedback.ShowPersistentMessage(ally.transform, "Intercession", new Color(.6f, .85f, 1f), 2.8f);
        return true;
    }
    public bool ToggleAura(SkillSO skill)
    {
        if (!Alive(Actor) || skill == null || skill.protection.kind != SkillProtectionKind.Rempart) return false;
        if (AuraSkill == skill) { ClearAura(); return true; }
        Actor.GetComponent<PlayerRegenerationEffect>()?.Clear();
        ClearAura(); AuraSkill = skill;
        auraLabel = CombatDamageWorldFeedback.ShowPersistentMessage(transform, "Rempart (" + skill.protection.radius + " m)", new Color(.6f, .85f, 1f), 3.15f);
        return true;
    }
    public void AddPrevented(float amount)
    {
        if (!Alive(Actor) || !IsJusticeUser || amount <= 0) return;
        var skill = JusticeSkill;
        Reserve = Mathf.Min(Actor.MaxHp * skill.protection.reserveMaxHealthFraction, Reserve + amount);
    }
    public int ConsumeReserve()
    {
        int damage = Mathf.RoundToInt(Reserve);
        Reserve = 0;
        return damage;
    }
    public void ResetReserve() => Reserve = 0;
    public void ApplyRemoteState(SkillSO aura, SkillSO link, SquadCharacterController ally, float remaining, float reserve)
    {
        if (CombatProtectionDamage.HasAuthority) return;
        if (AuraSkill != aura)
        {
            ClearAura(); AuraSkill = aura;
            if (aura != null) auraLabel = CombatDamageWorldFeedback.ShowPersistentMessage(transform, "Rempart (" + aura.protection.radius + " m)", new Color(.6f, .85f, 1f), 3.15f);
        }
        if (LinkSkill != link || LinkedAlly != ally)
        {
            ClearLink(); LinkSkill = link; LinkedAlly = ally;
            if (link != null && ally != null) linkLabel = CombatDamageWorldFeedback.ShowPersistentMessage(ally.transform, "Intercession", new Color(.6f, .85f, 1f), 2.8f);
        }
        linkUntil = Time.time + remaining; Reserve = reserve;
    }
    private void Update()
    {
        if (!Alive(Actor)) { Clear(); return; }
        if (LinkSkill != null && !HasLink) ClearLink();
        if (linkLabel != null) linkLabel.SetPersistentMessage("Intercession : " + LinkRemaining.ToString("0.0") + " s");
        if (Mathf.Abs(displayedReserve - Reserve) < .1f) return;
        displayedReserve = Reserve;
        if (Reserve < .5f) RemoveLabel(ref reserveLabel);
        else if (reserveLabel == null) reserveLabel = CombatDamageWorldFeedback.ShowPersistentMessage(transform,
            "Juste Retour : " + Mathf.RoundToInt(Reserve), new Color(1f, .85f, .35f), 3.5f);
        else reserveLabel.SetPersistentMessage("Juste Retour : " + Mathf.RoundToInt(Reserve));
    }
    public void ClearAura() { AuraSkill = null; RemoveLabel(ref auraLabel); }
    private void ClearLink() { LinkSkill = null; LinkedAlly = null; linkUntil = 0; RemoveLabel(ref linkLabel); }
    private void Clear() { ClearLink(); ClearAura(); ResetReserve(); RemoveLabel(ref reserveLabel); displayedReserve = -1; }
    private static void RemoveLabel(ref CombatDamageWorldFeedback label)
    {
        if (label != null) { label.gameObject.SetActive(false); Destroy(label.gameObject); }
        label = null;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
