using UnityEngine;

public static class CharacterLayerCollisionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureCharacterLayerCollisions()
    {
        IgnoreSelfCollision("Character");
        IgnoreSelfCollision("Player");
    }

    private static void IgnoreSelfCollision(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            return;
        }

        Physics.IgnoreLayerCollision(layer, layer, true);
        Physics2D.IgnoreLayerCollision(layer, layer, true);
    }
}
