using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Optional rendezvous. It never locks input or starts a Director itself.</summary>
[RequireComponent(typeof(NetworkObject),typeof(BoxCollider))]
public sealed class CycleGroupSequenceGate : NetworkBehaviour,ICharacterDetectedInteractable,ILocalInteractHandler
{
    public CycleController cycle;
    public string sequenceId;
    public float gatheringRadius=8, interactionDistance=2, confirmationTimeout=30, countdownSeconds=3;
    private readonly NetworkVariable<double> expiresAt=new(0), startsAt=new(0);
    private readonly NetworkVariable<int> participantCount=new(0);
    private readonly NetworkList<ulong> confirmations=new();
    private readonly HashSet<ulong> offlineConfirmations=new();
    private double offlineExpires,offlineStarts;
    private string roster="";
    private double nextWithdrawal;
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsServer;
    private double Now => Online ? NetworkManager.Singleton.ServerTime.Time : Time.unscaledTimeAsDouble;
    private double Expires => IsSpawned ? expiresAt.Value : offlineExpires;
    private double Starts => IsSpawned ? startsAt.Value : offlineStarts;
    private IEnumerable<ulong> Confirmed
    {
        get { if(IsSpawned){for(int i=0;i<confirmations.Count;i++)yield return confirmations[i];}else foreach(var id in offlineConfirmations)yield return id; }
    }
    public bool Pending => Expires>Now;
    public bool Ready => Pending && Starts>0 && Now>=Starts && AllReady();
    public static IEnumerable<(ulong id,Transform player)> LivingPlayers()
    {
        var manager=NetworkManager.Singleton;
        if(manager != null && manager.IsListening)
        {
            foreach(var id in manager.ConnectedClientsIds)
            { var player=NetcodePlayerUtils.GetPlayerTransform(id); if(Alive(player)) yield return(id,player); }
        }
        else { var player=LocalPlayerContext.LocalCharacterRoot; if(Alive(player))yield return(0,player); }
    }
    private static bool Alive(Transform player)
    {
        var c=player != null ? player.GetComponentInChildren<SquadCharacterController>() : null;
        if(c==null && player != null)c=player.GetComponentInParent<SquadCharacterController>();
        return c != null && c.CurrentHp>0;
    }
    private bool Within(Transform player) => Alive(player) && (player.position-transform.position).sqrMagnitude<=gatheringRadius*gatheringRadius;
    private bool Free(Transform player)
    {
        var character=player != null ? player.GetComponentInChildren<SquadCharacterController>() : null;
        if(character==null && player != null)character=player.GetComponentInParent<SquadCharacterController>();
        var bridge=character != null ? character.GetComponent<LitUccInteractionBridge>() : null;
        var movement=character != null ? character.GetComponent<LitOpsiveLocomotionBridge>() : null;
        return character != null && (movement==null || !movement.IsCombatLockActive && !movement.IsExternalLockActive) && (bridge==null || bridge.CanUseLitInteractable(this));
    }
    private void OnEnable() { LocalInputRouter.PrioritizedInteract+=ConfirmInput; LocalInputRouter.Return+=CancelInput; }
    private void ConfirmInput(InputAction.CallbackContext context)
    {
        if(!context.performed || !Pending || !Within(LocalPlayerContext.LocalCharacterRoot) || !Free(LocalPlayerContext.LocalCharacterRoot) || InputFocusStack.HasAnyFocus() || Online&&!IsSpawned)return;
        if(!LocalInputRouter.TryConsumeInteract())return;
        if(Online)ConfirmRpc();else Confirm(0,LocalPlayerContext.LocalCharacterRoot);
    }
    private void CancelInput(InputAction.CallbackContext context)
    {
        if(!context.performed || !Pending || !Within(LocalPlayerContext.LocalCharacterRoot) || InputFocusStack.HasAnyFocus())return;
        if(Online) { if(IsSpawned)CancelRpc(); } else Clear();
    }
    private bool AllReady()
    {
        var players=LivingPlayers().ToArray();
        return players.Length>0 && players.All(p=>Within(p.player)&&Free(p.player)&&Confirmed.Contains(p.id));
    }
    private void SetTimes(double expires,double starts)
    { if(IsSpawned){expiresAt.Value=expires;startsAt.Value=starts;}else{offlineExpires=expires;offlineStarts=starts;} }
    private void Clear()
    { SetTimes(0,0);if(IsSpawned)confirmations.Clear();else offlineConfirmations.Clear();roster=""; }
    private void OnDisable() { LocalInputRouter.PrioritizedInteract-=ConfirmInput; LocalInputRouter.Return-=CancelInput; if(Authority) Clear(); }
    private void Update()
    {
        if(Online && IsSpawned && Now>=nextWithdrawal && Confirmed.Contains(NetworkManager.Singleton.LocalClientId) &&
            (!Within(LocalPlayerContext.LocalCharacterRoot)||!Free(LocalPlayerContext.LocalCharacterRoot))){nextWithdrawal=Now+.25;WithdrawRpc();}
        if(!Authority || Online&&!IsSpawned)return;
        if(Expires<=0)return;
        if(!Pending || cycle==null || !cycle.IsSceneEventActive(CycleStepKind.SequenceCompleted,sequenceId)){Clear();return;}
        var players=LivingPlayers().ToArray();
        if(IsSpawned)participantCount.Value=players.Length;
        string next=string.Join(",",players.Select(p=>p.id).OrderBy(id=>id));
        if(roster!=next)
        {
            bool joined=roster.Length>0 && players.Any(p=>!roster.Split(',').Contains(p.id.ToString()));
            if(joined){if(IsSpawned)confirmations.Clear();else offlineConfirmations.Clear();}
            SetTimes(Expires,0);roster=next;
        }
        foreach(var id in Confirmed.ToArray())
            if(!players.Any(p=>p.id==id&&Within(p.player)&&Free(p.player))) { if(IsSpawned)confirmations.Remove(id);else offlineConfirmations.Remove(id);SetTimes(Expires,0); }
        if(AllReady())
        {
            if(Starts<=0)SetTimes(Expires,Now+(Online?countdownSeconds:0.001));
            if(Ready)cycle.RequestSceneSequence(sequenceId);
        }
        else if(Starts>0)SetTimes(Expires,0);
    }
    public bool TryConsume(out ulong[] audience)
    {
        audience=Array.Empty<ulong>();
        if(!Authority||!Ready)return false;
        audience=LivingPlayers().Select(p=>p.id).ToArray();Clear();return true;
    }
    public bool CanBeDetectedBy(SquadCharacterController c) => c != null && c.CurrentHp>0 && cycle != null &&
        cycle.IsSceneEventActive(CycleStepKind.SequenceCompleted,sequenceId) && CharacterInteractionDetection.IsInActiveFlameInfluence(this);
    public Collider GetInteractionDetectionCollider()=>GetComponent<Collider>();
    public Transform GetInteractionAnchor()=>transform;
    public float GetInteractionMaxDistance(SquadCharacterController _)=>interactionDistance;
    public int GetInteractionPriority(SquadCharacterController _)=>10;
    public void SetDetectedCharacter(GameObject _) { }
    public bool TryHandleLocalInteract()
    {
        var player=LocalPlayerContext.LocalCharacterRoot;
        if(!Within(player)||!CharacterInteractionDetection.IsCharacterWithinRange(player,GetComponent<Collider>(),transform,interactionDistance))return false;
        if(Online){if(!IsSpawned)return false;ConfirmRpc();return true;}
        return Confirm(0,player);
    }
    private bool Confirm(ulong id,Transform player)
    {
        if(!Authority||!Within(player)||!Free(player)||cycle==null||!cycle.IsSceneEventActive(CycleStepKind.SequenceCompleted,sequenceId)||!CharacterInteractionDetection.IsInActiveFlameInfluence(this))return false;
        var c=player.GetComponentInChildren<SquadCharacterController>();
        var bridge=c != null ? c.GetComponent<LitUccInteractionBridge>() : null;
        if(bridge != null&&!bridge.CanUseLitInteractable(this))return false;
        if(!Pending) { Clear();SetTimes(Now+confirmationTimeout,0); }
        if(!Confirmed.Contains(id)){if(IsSpawned)confirmations.Add(id);else offlineConfirmations.Add(id);}
        return true;
    }
    [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]
    private void ConfirmRpc(RpcParams rpc=default)=>Confirm(rpc.Receive.SenderClientId,NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId));
    [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]
    private void CancelRpc(RpcParams rpc=default) { if(LivingPlayers().Any(p=>p.id==rpc.Receive.SenderClientId))Clear(); }
    [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]
    private void WithdrawRpc(RpcParams rpc=default) { confirmations.Remove(rpc.Receive.SenderClientId);SetTimes(Expires,0); }
    private void OnGUI()
    {
        if(!Application.isPlaying||!Pending||!Within(LocalPlayerContext.LocalCharacterRoot)||InputFocusStack.HasAnyFocus())return;
        GUILayout.BeginArea(new Rect(Screen.width/2-180,Screen.height-165,360,155),GUI.skin.box);
        GUILayout.Label("Souvenir — Interagir : confirmer / Retour : annuler");
        GUILayout.Label($"Prêts : {Confirmed.Count()} / {(IsSpawned?participantCount.Value:LivingPlayers().Count())}");
        if(Starts>0)GUILayout.Label($"Projection dans {Math.Ceiling(Math.Max(0,Starts-Now))} s");
        if(GUILayout.Button("Je suis prêt")){if(Online)ConfirmRpc();else Confirm(0,LocalPlayerContext.LocalCharacterRoot);}
        if(GUILayout.Button("Annuler")){if(Online)CancelRpc();else Clear();}
        GUILayout.EndArea();
    }
}
