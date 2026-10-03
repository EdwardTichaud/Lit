using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject),typeof(BoxCollider))]
public sealed class ArdentEchoRelay : NetworkBehaviour,ICharacterDetectedInteractable,ILocalInteractHandler
{
    public CycleController cycle;
    public FalseChoirBoss boss;
    [Range(0,2)] public int relayIndex;
    public string sourceId;
    public Light pulseLight;
    public Transform handle;
    public float interactionDistance=2;
    private Collider detection;
    private Quaternion resting;
    private Renderer signalRenderer;
    private MaterialPropertyBlock signalProperties;
    private void Awake() { detection=GetComponent<Collider>(); if(handle != null) {resting=handle.localRotation;signalRenderer=handle.GetComponentInChildren<Renderer>();signalProperties=new MaterialPropertyBlock();} }
    private void OnEnable() => LocalInputRouter.Interact += CombatInteract;
    private void OnDisable() => LocalInputRouter.Interact -= CombatInteract;
    private void CombatInteract(InputAction.CallbackContext context)
    {
        var combat=RealTimeCombatManager.Instance;
        if(context.performed && !InputFocusStack.HasAnyFocus() && combat != null && combat.IsCombatActive &&
            boss != null && combat.EngagedEnemy==boss.Enemy && TryHandleLocalInteract()) LocalInputRouter.ConsumeInteract();
    }
    private void Update()
    {
        bool tuned=cycle != null && cycle.IsSceneStepCompleted(sourceId);
        bool signal=boss != null && boss.IsBossEngaged && !boss.IsExposed && boss.ActiveRelay==relayIndex;
        if(pulseLight != null) { pulseLight.enabled=tuned||signal; pulseLight.intensity=signal ? 3+Mathf.Sin(Time.unscaledTime*4)*2 : 1; }
        if(signalRenderer != null){signalRenderer.GetPropertyBlock(signalProperties);signalProperties.SetColor("_EmissiveColor",signal?Color.cyan*(3+Mathf.Sin(Time.unscaledTime*4)*2):tuned?Color.cyan*.15f:Color.black);signalRenderer.SetPropertyBlock(signalProperties);}
        if(handle != null) handle.localRotation=resting*Quaternion.Euler(tuned ? 45 : 0,0,0);
    }
    private bool HasAction => cycle != null && (cycle.IsSceneEventActive(CycleStepKind.Interaction,sourceId) || boss != null && boss.IsBossEngaged);
    public bool CanBeDetectedBy(SquadCharacterController c) => isActiveAndEnabled && c != null && c.CurrentHp>0 && HasAction && CharacterInteractionDetection.IsInActiveFlameInfluence(this);
    public Collider GetInteractionDetectionCollider() => detection != null ? detection : GetComponent<Collider>();
    public Transform GetInteractionAnchor() => transform;
    public float GetInteractionMaxDistance(SquadCharacterController _) => interactionDistance;
    public int GetInteractionPriority(SquadCharacterController _) => 10;
    public void SetDetectedCharacter(GameObject _) { }
    private bool Validate(Transform player)
    {
        var c=player != null ? player.GetComponentInChildren<SquadCharacterController>() : null;
        if(c==null && player != null) c=player.GetComponentInParent<SquadCharacterController>();
        var bridge=c != null ? c.GetComponent<LitUccInteractionBridge>() : null;
        return CanBeDetectedBy(c) && (bridge==null || bridge.CanUseLitInteractable(this)) && CharacterInteractionDetection.IsCharacterWithinRange(player,GetInteractionDetectionCollider(),transform,interactionDistance);
    }
    public bool TryHandleLocalInteract()
    {
        var player=LocalPlayerContext.LocalCharacterRoot;
        if(!Validate(player)) return false;
        if(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) { if(!IsSpawned)return false; UseRpc();return true; }
        Use(player);return true; // A rejected relay is still a consumed interaction, never a jump.
    }
    [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]
    private void UseRpc(RpcParams rpc=default) => Use(NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId));
    private bool Use(Transform player)
    {
        if(!Validate(player)) return false;
        if(cycle.IsSceneEventActive(CycleStepKind.Interaction,sourceId)) return cycle.TryReportSceneEvent(CycleStepKind.Interaction,sourceId);
        bool accepted=boss != null && boss.TryReveal(relayIndex);
        if(!accepted){if(IsSpawned)RejectedFeedbackRpc();else RejectedFeedback();}
        return accepted;
    }
    [Rpc(SendTo.ClientsAndHost)] private void RejectedFeedbackRpc()=>RejectedFeedback();
    private void RejectedFeedback()=>CombatDamageWorldFeedback.ShowMessage(transform,"Ce n’est pas le bon instant ou le bon relais",Color.cyan,2);
}
