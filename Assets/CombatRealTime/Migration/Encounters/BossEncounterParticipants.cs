using System;
using System.Collections.Generic;
using UnityEngine;

public static class BossEncounterParticipants
{
    // FalseChoir's existing gate validates every living player's arena position and all relays.
    public static bool UsesWholePartyGate(BossEncounterBehaviour boss) => boss is FalseChoirBoss;

    public static List<Transform> Collect(IEnumerable<Transform> livingPlayers, Func<Transform, bool> encounterGate,
        Func<Transform, bool> proximityGate, bool wholeParty)
    {
        var participants = new List<Transform>();
        if (livingPlayers == null || encounterGate == null || proximityGate == null) return participants;
        foreach (var player in livingPlayers)
            if (player != null && !participants.Contains(player) && encounterGate(player) && (wholeParty || proximityGate(player)))
                participants.Add(player);
        return participants;
    }
}
