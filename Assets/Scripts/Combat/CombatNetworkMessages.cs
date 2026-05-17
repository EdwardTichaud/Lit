using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Role: messages serialisables utilises par le combat en Netcode.
// Usage: transportes par CombatSessionManager via RPC pour entree, sortie et snapshots HUD.
// Responsibilities: convertir les strings en FixedString et serialiser les champs dans le meme ordre.
// Dependencies: Unity.Netcode, Unity.Collections.
// Precautions: l'ordre de NetworkSerialize doit rester identique entre lecture et ecriture.
/// <summary>
/// Helpers de conversion pour les chaines reseau du combat.
/// </summary>
internal static class CombatNetworkStrings
{
    /// <summary>
    /// Convertit une chaine courte en FixedString64Bytes tronquee proprement en UTF-8.
    /// </summary>
    public static FixedString64Bytes ToFixed64(string value)
    {
        return new FixedString64Bytes(TruncateUtf8(value, 48));
    }

    /// <summary>
    /// Convertit une chaine moyenne en FixedString128Bytes tronquee proprement en UTF-8.
    /// </summary>
    public static FixedString128Bytes ToFixed128(string value)
    {
        return new FixedString128Bytes(TruncateUtf8(value, 96));
    }

    /// <summary>
    /// Convertit un message long en FixedString512Bytes tronque proprement en UTF-8.
    /// </summary>
    public static FixedString512Bytes ToFixed512(string value)
    {
        return new FixedString512Bytes(TruncateUtf8(value, 384));
    }

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string result = value;
        // On retire un caractere a la fois pour ne jamais couper au milieu d'un caractere UTF-8.
        while (result.Length > 0 && Encoding.UTF8.GetByteCount(result) > maxBytes)
        {
            result = result.Substring(0, result.Length - 1);
        }

        return result;
    }
}

/// <summary>
/// Donnees envoyees quand un client entre en combat.
/// </summary>
public struct CombatEnterData : INetworkSerializable
{
    /// <summary>Identifiant de session combat.</summary>
    public FixedString64Bytes SessionId;
    /// <summary>Position du joueur pendant la presentation combat.</summary>
    public Vector3 PlayerCombatPosition;
    /// <summary>Rotation du joueur pendant la presentation combat.</summary>
    public Quaternion PlayerCombatRotation;
    /// <summary>Indique si l'ennemi possede aussi une presentation de placement.</summary>
    public bool HasEnemyPresentation;
    /// <summary>Position ou l'ennemi retourne apres combat.</summary>
    public Vector3 EnemyReturnPosition;
    /// <summary>Rotation de retour de l'ennemi.</summary>
    public Quaternion EnemyReturnRotation;
    /// <summary>Position de presentation combat de l'ennemi.</summary>
    public Vector3 EnemyCombatPosition;
    /// <summary>Rotation de presentation combat de l'ennemi.</summary>
    public Quaternion EnemyCombatRotation;

    /// <summary>
    /// Cree un message d'entree en combat.
    /// </summary>
    public CombatEnterData(
        string sessionId,
        Vector3 playerCombatPosition,
        Quaternion playerCombatRotation,
        bool hasEnemyPresentation,
        Vector3 enemyReturnPosition,
        Quaternion enemyReturnRotation,
        Vector3 enemyCombatPosition,
        Quaternion enemyCombatRotation)
    {
        SessionId = CombatNetworkStrings.ToFixed64(sessionId);
        PlayerCombatPosition = playerCombatPosition;
        PlayerCombatRotation = playerCombatRotation;
        HasEnemyPresentation = hasEnemyPresentation;
        EnemyReturnPosition = enemyReturnPosition;
        EnemyReturnRotation = enemyReturnRotation;
        EnemyCombatPosition = enemyCombatPosition;
        EnemyCombatRotation = enemyCombatRotation;
    }

    /// <summary>
    /// Serialise les donnees d'entree dans l'ordre attendu par Netcode.
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SessionId);
        serializer.SerializeValue(ref PlayerCombatPosition);
        serializer.SerializeValue(ref PlayerCombatRotation);
        serializer.SerializeValue(ref HasEnemyPresentation);
        serializer.SerializeValue(ref EnemyReturnPosition);
        serializer.SerializeValue(ref EnemyReturnRotation);
        serializer.SerializeValue(ref EnemyCombatPosition);
        serializer.SerializeValue(ref EnemyCombatRotation);
    }
}

/// <summary>
/// Donnees envoyees quand un combat se termine.
/// </summary>
public struct CombatExitData : INetworkSerializable
{
    /// <summary>Identifiant de session combat.</summary>
    public FixedString64Bytes SessionId;
    /// <summary>Message final affiche au HUD.</summary>
    public FixedString512Bytes Message;
    /// <summary>Position ou le joueur retourne apres la transition.</summary>
    public Vector3 PlayerReturnPosition;
    /// <summary>Rotation de retour du joueur.</summary>
    public Quaternion PlayerReturnRotation;
    /// <summary>PV du joueur a la sortie du combat.</summary>
    public int PlayerHp;
    /// <summary>PV max du joueur a la sortie du combat.</summary>
    public int PlayerMaxHp;
    /// <summary>Indique si un ennemi doit aussi etre replace.</summary>
    public bool HasEnemyPresentation;
    /// <summary>Position de retour de l'ennemi.</summary>
    public Vector3 EnemyReturnPosition;
    /// <summary>Rotation de retour de l'ennemi.</summary>
    public Quaternion EnemyReturnRotation;
    /// <summary>PV restants de l'ennemi principal.</summary>
    public int EnemyRemainingHp;
    /// <summary>Vrai si le joueur a gagne.</summary>
    public bool PlayerVictory;

    /// <summary>
    /// Cree un message de sortie de combat.
    /// </summary>
    public CombatExitData(
        string sessionId,
        string message,
        Vector3 playerReturnPosition,
        Quaternion playerReturnRotation,
        int playerHp,
        int playerMaxHp,
        bool hasEnemyPresentation,
        Vector3 enemyReturnPosition,
        Quaternion enemyReturnRotation,
        int enemyRemainingHp,
        bool playerVictory)
    {
        SessionId = CombatNetworkStrings.ToFixed64(sessionId);
        Message = CombatNetworkStrings.ToFixed512(message);
        PlayerReturnPosition = playerReturnPosition;
        PlayerReturnRotation = playerReturnRotation;
        PlayerHp = playerHp;
        PlayerMaxHp = playerMaxHp;
        HasEnemyPresentation = hasEnemyPresentation;
        EnemyReturnPosition = enemyReturnPosition;
        EnemyReturnRotation = enemyReturnRotation;
        EnemyRemainingHp = enemyRemainingHp;
        PlayerVictory = playerVictory;
    }

    /// <summary>
    /// Serialise les donnees de sortie dans l'ordre attendu par Netcode.
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SessionId);
        serializer.SerializeValue(ref Message);
        serializer.SerializeValue(ref PlayerReturnPosition);
        serializer.SerializeValue(ref PlayerReturnRotation);
        serializer.SerializeValue(ref PlayerHp);
        serializer.SerializeValue(ref PlayerMaxHp);
        serializer.SerializeValue(ref HasEnemyPresentation);
        serializer.SerializeValue(ref EnemyReturnPosition);
        serializer.SerializeValue(ref EnemyReturnRotation);
        serializer.SerializeValue(ref EnemyRemainingHp);
        serializer.SerializeValue(ref PlayerVictory);
    }
}

/// <summary>
/// Snapshot court envoye regulierement pour mettre a jour le HUD de combat.
/// </summary>
public struct CombatSnapshotData : INetworkSerializable
{
    /// <summary>Identifiant de session combat.</summary>
    public FixedString64Bytes SessionId;
    /// <summary>Etat de tour encode en byte pour le reseau.</summary>
    public byte Turn;
    /// <summary>Temps restant du tour cote serveur.</summary>
    public float TimerRemaining;
    /// <summary>PV courants du joueur.</summary>
    public int PlayerHp;
    /// <summary>PV max du joueur.</summary>
    public int PlayerMaxHp;
    /// <summary>Nom de l'ennemi principal affiche.</summary>
    public FixedString128Bytes EnemyName;
    /// <summary>PV courants de l'ennemi principal.</summary>
    public int EnemyHp;
    /// <summary>PV max de l'ennemi principal.</summary>
    public int EnemyMaxHp;
    /// <summary>Nombre d'ennemis encore vivants.</summary>
    public int AliveEnemies;
    /// <summary>Nombre total d'ennemis dans la session.</summary>
    public int TotalEnemies;
    /// <summary>Nombre de prieres de soutien actives.</summary>
    public int PrayerSupportCount;
    /// <summary>Reduction de degats calculee pour le joueur.</summary>
    public float DamageReduction;
    /// <summary>Indique si le bouton d'action joueur doit etre bloque.</summary>
    public bool PlayerActionLocked;
    /// <summary>Message courant du combat.</summary>
    public FixedString512Bytes Message;

    /// <summary>
    /// Cree un snapshot complet pour le HUD de combat.
    /// </summary>
    public CombatSnapshotData(
        string sessionId,
        CombatTurn turn,
        float timerRemaining,
        int playerHp,
        int playerMaxHp,
        string enemyName,
        int enemyHp,
        int enemyMaxHp,
        int aliveEnemies,
        int totalEnemies,
        int prayerSupportCount,
        float damageReduction,
        bool playerActionLocked,
        string message)
    {
        SessionId = CombatNetworkStrings.ToFixed64(sessionId);
        Turn = (byte)turn;
        TimerRemaining = timerRemaining;
        PlayerHp = playerHp;
        PlayerMaxHp = playerMaxHp;
        EnemyName = CombatNetworkStrings.ToFixed128(enemyName);
        EnemyHp = enemyHp;
        EnemyMaxHp = enemyMaxHp;
        AliveEnemies = aliveEnemies;
        TotalEnemies = totalEnemies;
        PrayerSupportCount = prayerSupportCount;
        DamageReduction = damageReduction;
        PlayerActionLocked = playerActionLocked;
        Message = CombatNetworkStrings.ToFixed512(message);
    }

    /// <summary>
    /// Etat de tour reconverti depuis la valeur reseau.
    /// </summary>
    public CombatTurn TurnState => (CombatTurn)Turn;

    /// <summary>
    /// Serialise le snapshot dans l'ordre attendu par Netcode.
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SessionId);
        serializer.SerializeValue(ref Turn);
        serializer.SerializeValue(ref TimerRemaining);
        serializer.SerializeValue(ref PlayerHp);
        serializer.SerializeValue(ref PlayerMaxHp);
        serializer.SerializeValue(ref EnemyName);
        serializer.SerializeValue(ref EnemyHp);
        serializer.SerializeValue(ref EnemyMaxHp);
        serializer.SerializeValue(ref AliveEnemies);
        serializer.SerializeValue(ref TotalEnemies);
        serializer.SerializeValue(ref PrayerSupportCount);
        serializer.SerializeValue(ref DamageReduction);
        serializer.SerializeValue(ref PlayerActionLocked);
        serializer.SerializeValue(ref Message);
    }
}
