using System;
using System.Linq; // Permet de concaténer facilement les tableaux de composants
using UnityEngine;
using UnityEngine.Rendering; // Nécessaire pour manipuler les Rendering Layer par nom

/// <summary>
/// Permet d'appliquer automatiquement un <c>Rendering Layer</c>
/// différent à chaque objet de la hiérarchie (meshs comme terrains)
/// en fonction de son <see cref="LayerMask"/> Unity.
/// </summary>
public class SetRenderingLayer : MonoBehaviour
{
    public static SetRenderingLayer Instance { get; private set; }

    /// <summary>
    /// Association entre un ou plusieurs <see cref="LayerMask"/> Unity et
    /// le <c>Rendering Layer</c> à appliquer, désigné par son nom afin de
    /// faciliter la lecture et la maintenance.

    /// </summary>
    [Serializable]
    public struct LayerRenderingPair
    {
        [Tooltip("Couches Unity à cibler")]
        public LayerMask layers;

        /// <summary>
        /// Nom du Rendering Layer à appliquer à la cible.
        /// L'utilisation d'une chaîne évite de mémoriser les indices numériques.
        /// </summary>
        [Tooltip("Nom du Rendering Layer à appliquer")]
        public string renderingLayerName;
    }

    [Tooltip("Liste des associations LayerMask / Rendering Layer.")]
    public LayerRenderingPair[] mappings;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Applique les Rendering Layers aux enfants au démarrage
        // pour s'assurer que tout est bien configuré dès le début.
        ApplyToAll();
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les composants fournis.
    /// Les terrains ne dérivant pas de <see cref="Renderer"/>, on utilise
    /// un tableau générique de <see cref="Component"/> pour traiter à la fois
    /// les meshs classiques (<c>Renderer</c>) et les terrains (<c>Terrain</c>).
    /// Cette méthode factorise la logique utilisée par
    /// <see cref="ApplyToChildren"/> et <see cref="ApplyToAll"/> afin d'éviter
    /// toute duplication de code et de simplifier la maintenance.
    /// </summary>
    /// <param name="components">Composants à analyser (Renderer et/ou Terrain).</param>
    /// <returns>Nombre de composants effectivement modifiés.</returns>
    private int ApplyRenderingLayersTo(Component[] components)
    {
        // Vérifie qu'au moins une association a été fournie.
        if (mappings == null || mappings.Length == 0)
        {
            Debug.LogWarning("Aucune association LayerMask/RenderingLayer n'a été définie.");
            return 0;
        }

        int count = 0; // Nombre de composants modifiés

        // Parcourt chaque composant à traiter (Renderer ou Terrain)
        foreach (var component in components)
        {
            // Parcours toutes les associations pour trouver celle correspondant à la couche de l'objet
            foreach (var mapping in mappings)
            {
                // Récupération de la couche Unity de l'objet courant
                if (((1 << component.gameObject.layer) & mapping.layers.value) != 0)
                {
                    // Récupère le masque correspondant au nom du Rendering Layer demandé
                    uint mask = RenderingLayerMask.GetMask(mapping.renderingLayerName);

                    if (mask == 0)
                    {
                        // Si aucun masque n'a été trouvé, on avertit et on ne modifie pas cet objet
                        Debug.LogWarning($"Rendering Layer \"{mapping.renderingLayerName}\" introuvable pour {component.gameObject.name}.");
                        break;
                    }

                    // Application du masque de rendu trouvé en fonction du type de composant
                    switch (component)
                    {
                        case Renderer r: // Pour les meshs classiques
                            r.renderingLayerMask = mask;
                            count++;
                            break;

                        case Terrain t: // Pour les terrains Unity
                            // Assigne le Rendering Layer au terrain lui‑même
                            t.renderingLayerMask = mask;
                            count++;

                            // --- Gestion spécifique des arbres du terrain ---
                            // Les arbres peints sur un Terrain ne sont pas des
                            // GameObjects présents dans la scène : ils sont
                            // générés à partir des "TreePrototypes" stockés dans
                            // le TerrainData. Leur Rendering Layer est donc
                            // déterminé par le prefab associé au prototype.
                            var prototypes = t.terrainData?.treePrototypes;
                            if (prototypes != null && prototypes.Length > 0)
                            {
                                bool updated = false; // Indique si au moins un arbre a été modifié

                                foreach (var proto in prototypes)
                                {
                                    var prefab = proto.prefab;
                                    if (prefab == null)
                                        continue; // Sécurité en cas de prototype manquant

                                    // Vérifie si le Layer du prefab correspond à la règle courante
                                    if (((1 << prefab.layer) & mapping.layers.value) == 0)
                                        continue;

                                    // Applique le Rendering Layer à tous les Renderers du prefab
                                    foreach (var rProto in prefab.GetComponentsInChildren<Renderer>(true))
                                    {
                                        rProto.renderingLayerMask = mask;
                                    }

                                    updated = true;
                                    count++; // Comptabilise le prototype traité
                                }

                                // Force la mise à jour visuelle si des prototypes ont été modifiés
                                if (updated)
                                {
                                    // Réaffecte le tableau de prototypes au TerrainData.
                                    // Sans cette étape, Unity ne détecte pas toujours
                                    // les modifications et conserve l'ancien Rendering Layer
                                    // pour les instances d'arbres déjà générées.
                                    t.terrainData.treePrototypes = prototypes;

                                    // Actualise les prototypes pour que les nouvelles
                                    // valeurs soient prises en compte par le Terrain.
                                    t.terrainData.RefreshPrototypes();

                                    // Recharge les représentations internes du Terrain
                                    // afin que l'éclairage soit correctement appliqué
                                    // aux arbres modifiés.
                                    t.Flush();
                                }
                            }

                            break;
                    }

                    break; // On s'arrête dès qu'une correspondance est trouvée
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/>
    /// et <see cref="Terrain"/> présents dans les enfants de ce
    /// <see cref="GameObject"/>.
    /// </summary>
    public void ApplyToChildren()
    {
        // Récupère tous les Renderer et Terrain présents dans la hiérarchie
        var components = GetComponentsInChildren<Renderer>(true)
            .Cast<Component>()
            .Concat(GetComponentsInChildren<Terrain>(true))
            .ToArray();

        int count = ApplyRenderingLayersTo(components);
        Debug.Log($"✅ Rendering Layer(s) appliqué(s) à {count} composant(s) de la hiérarchie.");
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/>
    /// et <see cref="Terrain"/> actuellement présents dans la scène entière.
    /// </summary>
    public void ApplyToAll()
    {
        // Récupère tous les Renderer et Terrain présents dans la scène complète
        // Utilise la nouvelle API FindObjectsByType pour inclure les objets inactifs
        var components = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Cast<Component>()
            .Concat(FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            .ToArray();

        int count = ApplyRenderingLayersTo(components);
        Debug.Log($"🌐 Rendering Layer(s) appliqué(s) à {count} composant(s) de la scène.");
    }
}
