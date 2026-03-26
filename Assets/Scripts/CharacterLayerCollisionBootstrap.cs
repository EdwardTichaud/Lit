using UnityEngine;

public static class CharacterLayerCollisionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureCharacterLayerCollisions()
    {
        int characterLayer = LayerMask.NameToLayer("Character");
        if (characterLayer < 0)
        {
            return;
        }

        Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);
        Physics2D.IgnoreLayerCollision(characterLayer, characterLayer, true);
    }
}
