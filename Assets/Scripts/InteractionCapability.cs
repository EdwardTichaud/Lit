using System;

// Capacites data-driven accordees par les items equipes pour interagir avec le monde.
[Flags]
public enum InteractionCapability
{
    None = 0,
    BreakWeakWall = 1 << 0,
    BreakRock = 1 << 1,
    OpenSpecialMechanism = 1 << 2,
}
