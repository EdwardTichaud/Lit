using UnityEngine;

public static class RuntimeUiSpriteUtility
{
    private static Sprite solidSprite;

    public static Sprite SolidSprite
    {
        get
        {
            if (solidSprite == null)
            {
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();

                solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
                solidSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return solidSprite;
        }
    }
}
