using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

internal static class CombatNetworkStrings
{
    public static FixedString64Bytes ToFixed64(string value)
    {
        return new FixedString64Bytes(TruncateUtf8(value, 48));
    }

    public static FixedString128Bytes ToFixed128(string value)
    {
        return new FixedString128Bytes(TruncateUtf8(value, 96));
    }

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
        while (result.Length > 0 && Encoding.UTF8.GetByteCount(result) > maxBytes)
        {
            result = result.Substring(0, result.Length - 1);
        }

        return result;
    }
}

public struct CombatEnterData : INetworkSerializable
{
    public FixedString64Bytes SessionId;
    public Vector3 PlayerCombatPosition;
    public Quaternion PlayerCombatRotation;
    public bool HasEnemyPresentation;
    public Vector3 EnemyReturnPosition;
    public Quaternion EnemyReturnRotation;
    public Vector3 EnemyCombatPosition;
    public Quaternion EnemyCombatRotation;

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

public struct CombatExitData : INetworkSerializable
{
    public FixedString64Bytes SessionId;
    public FixedString512Bytes Message;
    public Vector3 PlayerReturnPosition;
    public Quaternion PlayerReturnRotation;
    public int PlayerHp;
    public int PlayerMaxHp;
    public bool HasEnemyPresentation;
    public Vector3 EnemyReturnPosition;
    public Quaternion EnemyReturnRotation;
    public int EnemyRemainingHp;
    public bool PlayerVictory;

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

public struct CombatSnapshotData : INetworkSerializable
{
    public FixedString64Bytes SessionId;
    public byte Turn;
    public float TimerRemaining;
    public int PlayerHp;
    public int PlayerMaxHp;
    public FixedString128Bytes EnemyName;
    public int EnemyHp;
    public int EnemyMaxHp;
    public int AliveEnemies;
    public int TotalEnemies;
    public int PrayerSupportCount;
    public float DamageReduction;
    public bool PlayerActionLocked;
    public FixedString512Bytes Message;

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

    public CombatTurn TurnState => (CombatTurn)Turn;

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
