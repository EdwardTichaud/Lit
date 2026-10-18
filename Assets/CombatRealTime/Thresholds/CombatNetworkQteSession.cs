using System;
using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEngine;

public enum CombatNetworkQteButton : byte { Y, B, A, X }
public enum CombatNetworkQteResult : byte { Rejected, Released, Success, Failure, Expired }

/// <summary>
/// Server-side participant reservation and QTE-window validation. Actor-owned transport
/// routes authenticated presses; CombatHealthThresholdController owns authored playback and outcomes.
/// </summary>
public sealed class CombatNetworkQteSession : IDisposable
{
    private CombatCommandAuthority authority;
    private NetworkObject source;
    private NetworkObject target;
    private Func<ulong, bool> isAssigned;
    private CombatCinematicReservationLease lease;
    private ulong owner;
    private ulong lastInputSequence;
    private double expiresAt;
    private double deadline;
    private bool open;
    private bool awaitingRelease;
    private CombatNetworkQteButton expected;

    public CombatActionTicket Ticket { get; private set; }
    public ulong Window { get; private set; }
    public NetworkObject Source => source;

    private CombatNetworkQteSession() { }

    // isAssigned is a trusted, live business-assignment predicate, not client data.
    public static bool TryCreate(CombatCommandAuthority authority, NetworkObject source,
        NetworkObject target, ICombatTarget targetActor, Func<ulong, bool> isAssigned,
        out CombatNetworkQteSession session)
    {
        session = null;
        if (authority == null || !authority.IsAuthoritative || source == null || target == null ||
            isAssigned == null || !Finite(Time.unscaledTimeAsDouble)) return false;
        var candidate = new CombatNetworkQteSession
        {
            authority = authority, source = source, target = target, isAssigned = isAssigned,
            owner = source.OwnerClientId, expiresAt = Time.unscaledTimeAsDouble + 60
        };
        if (!candidate.ValidSource() ||
            !authority.TryAcquire(candidate, source, target, targetActor, out candidate.lease)) return false;
        candidate.Ticket = candidate.lease.Ticket;
        candidate.lease.Released += candidate.OnReleased;
        session = candidate;
        return true;
    }

    public bool IsActive
    {
        get
        {
            if (lease == null || !lease.IsActive) return false;
            if (ValidSource() && lease.ParticipantsAlive() && Finite(Time.unscaledTimeAsDouble) &&
                Time.unscaledTimeAsDouble < expiresAt) return true;
            Dispose();
            return false;
        }
    }

    // Only authoritative animation/sequence code opens a window. Input cannot open it.
    public bool TryOpenWindow(CombatNetworkQteButton button, double durationSeconds, bool requireRelease = true)
    {
        if (!IsActive || open || Window == ulong.MaxValue || !ValidButton(button) ||
            !Finite(durationSeconds) || durationSeconds <= 0 || durationSeconds > 60) return false;
        double now = Time.unscaledTimeAsDouble;
        if (now + durationSeconds > expiresAt) return false;
        Window++;
        expected = button;
        deadline = now + durationSeconds;
        awaitingRelease = requireRelease;
        open = true;
        return true;
    }

    // senderClientId MUST come from RPC receive metadata. There is no client timestamp.
    public CombatNetworkQteResult Submit(ulong senderClientId, CombatActionTicket ticket,
        ulong window, ulong inputSequence, CombatNetworkQteButton button, bool pressed)
    {
        if (!IsActive || senderClientId != owner || !Same(ticket, Ticket) || window != Window ||
            !open || inputSequence == 0 || inputSequence <= lastInputSequence || !ValidButton(button))
            return CombatNetworkQteResult.Rejected;
        if (Poll() == CombatNetworkQteResult.Expired) return CombatNetworkQteResult.Expired;
        lastInputSequence = inputSequence;
        if (!pressed)
        {
            if (button != expected) return CombatNetworkQteResult.Rejected;
            awaitingRelease = false;
            return CombatNetworkQteResult.Released;
        }
        if (awaitingRelease) return CombatNetworkQteResult.Rejected;
        open = false;
        return button == expected ? CombatNetworkQteResult.Success : CombatNetworkQteResult.Failure;
    }

    // The executor must poll expiry and decide its authored failure outcome.
    public CombatNetworkQteResult Poll()
    {
        if (!IsActive || !open || Time.unscaledTimeAsDouble < deadline) return CombatNetworkQteResult.Rejected;
        open = false;
        return CombatNetworkQteResult.Expired;
    }

    private bool ValidSource()
    {
        try
        {
            var network = NetworkManager.Singleton;
            return authority != null && authority.IsAuthoritative && network != null && network.IsListening &&
                network.IsServer && !network.ShutdownInProgress && network.GetComponent<CombatCommandAuthority>() == authority &&
                source != null && target != null && source.NetworkManager == network && target.NetworkManager == network &&
                source.IsSpawned && target.IsSpawned && source.OwnerClientId == owner &&
                network.ConnectedClients.ContainsKey(owner) && authority.IsRegistered(source) &&
                authority.IsRegistered(target) && isAssigned(owner);
        }
        catch { return false; }
    }

    private void OnReleased() { open = false; lease = null; }

    public void Dispose()
    {
        var held = lease;
        lease = null;
        open = false;
        if (held == null) return;
        held.Released -= OnReleased;
        held.Release();
    }

    private static bool ValidButton(CombatNetworkQteButton button) => button >= CombatNetworkQteButton.Y && button <= CombatNetworkQteButton.X;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Same(CombatActionTicket a, CombatActionTicket b) =>
        a.AuthorityEpoch == b.AuthorityEpoch && a.ActorId == b.ActorId && a.Generation == b.Generation;
}
