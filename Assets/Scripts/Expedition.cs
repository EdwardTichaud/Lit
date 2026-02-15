using UnityEngine;

[CreateAssetMenu(fileName = "Expedition", menuName = "Scriptable Objects/Expedition")]
// Donnees d'une expedition (UI + tag de scene associe).
public class Expedition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Nom affiche pour l'expedition.")]
    public string expeditionName;
    [Tooltip("Icone de l'expedition.")]
    public Sprite expeditionSprite;
    [TextArea, Tooltip("Description affichee dans le panel.")]
    public string description;
    [Tooltip("Si false, l'expedition est verouillee.")]
    public bool unlocked = true;
    [Tooltip("Tag de la scene/labyrinthe associe.")]
    public string expeditionTag;

    public GameObject FindLabyrinthRoot()
    {
        if (string.IsNullOrWhiteSpace(expeditionTag))
        {
            return null;
        }

        try
        {
            return GameObject.FindGameObjectWithTag(expeditionTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Expedition: tag introuvable \"{expeditionTag}\" pour {name}.");
            return null;
        }
    }
}
