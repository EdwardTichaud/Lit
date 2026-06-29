using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryPanelManager : MonoBehaviour
{
    public Image xpIcon;
    public TextMeshProUGUI xpText;
    public Transform itemsContainer;
    public TextMeshProUGUI itemsText;
    public TextMeshProUGUI enemiesText;
    public TextMeshProUGUI maxDamageText;
    // Image qui affichera le portrait de l'unité MVP
    public Image mvpImage;
    public TextMeshProUGUI timeText;

    void Awake()
    {
        // L'animation du panneau doit être indépendante du timeScale
        Animator anim = GetComponent<Animator>();
        if (anim != null)
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (xpText == null)
        {
            xpText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("xp"));
        }

        if (xpIcon == null)
        {
            xpIcon = GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name.ToLower().Contains("xpicon"));
        }
        if (itemsContainer == null)
        {
            var tr = transform
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("items"));
            if (tr != null) itemsContainer = tr;
        }
        if (enemiesText == null)
        {
            enemiesText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("ennemi"));
        }
        if (maxDamageText == null)
        {
            maxDamageText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("degats"));
        }
        // Recherche spécifique de l'image "MVP_Portrait" afin d'éviter de
        // référencer par erreur l'image de fond "MVP_Image"
        mvpImage = GetComponentsInChildren<Image>(true)
            .FirstOrDefault(i => i.name == "MVP_Portrait");
        if (timeText == null)
        {
            timeText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("temps"));
        }
    }

    public void DisplayVictory(int xp, List<ItemData> items, int totalEnemies, float duration, CharacterUnit mvp, int maxDamage)
    {
        // Affiche le gain d'expérience du groupe
        if (xpText != null)
            xpText.text = $"+{xp} XP";
        if (xpIcon != null)
            xpIcon.enabled = xp > 0;

        // Liste les objets récupérés dans le conteneur prévu à cet effet
        if (itemsContainer != null)
        {
            // Nettoyage des anciens éléments avant affichage des nouveaux
            foreach (Transform child in itemsContainer)
                Destroy(child.gameObject);

            foreach (var item in items)
            {
                // Création d'une entrée visuelle pour chaque objet
                GameObject entry = new GameObject(item.itemName);
                entry.transform.SetParent(itemsContainer, false);

                // Icône de l'objet
                var img = entry.AddComponent<Image>();
                img.sprite = item.itemIcon;

                // Nom de l'objet
                var txtObj = new GameObject("Text");
                txtObj.transform.SetParent(entry.transform, false);
                var txt = txtObj.AddComponent<TextMeshProUGUI>();
                txt.text = item.itemName;
            }
        }

        // Affiche le nombre d'ennemis vaincus durant ce combat
        if (enemiesText != null)
            enemiesText.text = $"Ennemis vaincus : {totalEnemies}";

        // Indique le plus gros montant de dégâts infligés en un tour
        if (maxDamageText != null)
            maxDamageText.text = $"Dégâts max : {maxDamage}";

        // Affiche le portrait de l'unité ayant infligé ces dégâts
        if (mvpImage != null && mvp != null && mvp.Data.portrait != null)
            mvpImage.sprite = mvp.Data.portrait;

        // Durée totale du combat
        if (timeText != null)
            timeText.text = System.TimeSpan.FromSeconds(duration).ToString("mm':'ss");

        if (itemsText == null)
        {
            itemsText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("item"));
        }
    }

    public void DisplayRewards(int xp, List<ItemData> items)
    {
        // Affiche de manière concise les récompenses, utilisé pour des cas simples
        if (xpText != null)
            xpText.text = $"+{xp} XP";

        if (itemsText != null)
            itemsText.text = items.Count > 0
                ? string.Join(", ", items.Select(i => i.itemName))
                : string.Empty;
    }
}
