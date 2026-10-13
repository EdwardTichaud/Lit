using System;
using UnityEngine;

public enum SkillControlKind { None, Revelation, Entrave, PasDeRecul }

[Serializable]
public sealed class SkillControlProfile
{
    public SkillControlKind kind;
    [Min(0)] public float range = 25f;
    [Min(.01f)] public float durationSeconds = 6f;
    [Min(0)] public float radius = 4f;
    [Range(0, 1)] public float strength = .2f;
    [Min(0)] public float distance = 5f;
}
