using System;
using System.Collections.Generic;
using UnityEngine;

public partial class NewBattleManager
{
    #region Initialisation du champs de bataille
    public void SpawnAll()
    {
        // Nettoie les références nulles pouvant persister après un précédent combat
        activeCharacterUnits.RemoveAll(u => u == null);
        unitsInBattle.RemoveAll(u => u == null);

        // Si des unités valides existent encore, on évite de doubler le spawn
        bool hasLiveUnits = unitsInBattle.Exists(u => u != null && u.gameObject.activeInHierarchy);
        if (hasLiveUnits || activeCharacterUnits.Exists(u => u != null && u.gameObject.activeInHierarchy))
        {
            Debug.LogWarning("[NewBattleManager] SpawnAll déjà exécuté ou unités déjà présentes.");
            return;
        }

        // S'assure que les listes sont complètement vides avant d'instancier de nouvelles unités
        activeCharacterUnits.Clear();
        unitsInBattle.Clear();
        SpawnSquadUnits();
        SpawnEnemies();
    }

    /// <summary>
    /// Assure la résolution (et la mise en cache) d'un point de spawn afin d'éviter les recherches répétées.
    /// </summary>
    private Transform ResolveSpawnRoot(ref Transform cache, string requiredTag)
    {
        if (cache != null)
            return cache;

        var taggedRoot = GameObject.FindGameObjectWithTag(requiredTag);
        if (taggedRoot == null)
        {
            Debug.LogWarning($"[NewBattleManager] Aucun objet avec le tag '{requiredTag}' n'a été trouvé dans la scène.");
            return null;
        }

        cache = taggedRoot.transform;
        return cache;
    }

    private void SpawnSquadUnits()
    {
        playerSpawnPoints.Clear();
        var spawnRoot = ResolveSpawnRoot(ref playerSpawnRoot, "PlayerSpawn");
        if (spawnRoot == null)
            return;

        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            var child = spawnRoot.GetChild(i);
            if (child != null)
            {
                playerSpawnPoints.Add(child);
            }
        }

        // Seuls les trois premiers membres de la squad peuvent participer au combat
        var squad = SquadManager.Instance != null ? SquadManager.Instance.SquadCharacters : new List<CharacterData>();
        var availableSquad = new List<CharacterData>();
        var allegianceManager = AllegianceManager.Instance;
        foreach (var member in squad)
        {
            if (member == null)
                continue;

            if (allegianceManager != null && allegianceManager.GetEffectiveAllegiance(member) == AllegianceSide.Enemy)
                continue;

            availableSquad.Add(member);
        }

        int maxSquadMembers = Mathf.Min(3, availableSquad.Count);
        for (int i = 0; i < maxSquadMembers && i < playerSpawnPoints.Count; i++)
        {
            var pc = availableSquad[i];
            var spawnPoint = playerSpawnPoints[i];

            if (pc.characterBattleModel == null)
            {
                Debug.LogWarning($"[SpawnPlayers] Aucun modèle défini pour {pc.characterName}, annulation du spawn.");
                continue;
            }

            // 🧍 Apparition immédiate de l'unité à sa position de combat.
            //     Nous utilisons désormais une méthode dédiée afin d'appliquer
            //     automatiquement une hauteur supplémentaire pour les unités
            //     aériennes, garantissant que les modèles ne "tombent" plus sur
            //     le sol lorsque la scène démarre.
            var spawnPosition = ComputeSpawnPosition(pc, spawnPoint);
            var unitGO = Instantiate(pc.characterBattleModel, spawnPosition, Quaternion.identity);
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"SquadUnit_{i}";

            // ✅ Génère l'effet visuel du rayon directement à l'emplacement final.
            if (squadUnitRay != null)
                Instantiate(squadUnitRay, spawnPosition, Quaternion.identity);

            var unit = unitGO.GetComponent<CharacterUnit>();
            unit.Initialize(pc);
            unitsInBattle.Add(unit);
        }
    }

    private void SpawnEnemies()
    {
        enemySpawnPoints.Clear();
        var spawnRoot = ResolveSpawnRoot(ref enemySpawnRoot, "EnemySpawn");
        if (spawnRoot == null)
            return;

        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            var child = spawnRoot.GetChild(i);
            if (child != null)
            {
                enemySpawnPoints.Add(child);
            }
        }

        for (int i = 0; i < enemyTemplates.Count && i < enemySpawnPoints.Count; i++)
        {
            var template = enemyTemplates[i];
            var enemyData = Instantiate(template);
            var spawnPoint = enemySpawnPoints[i];

            if (enemyData.characterBattleModel == null)
            {
                Debug.LogWarning($"[SpawnEnemies] Aucun modèle défini pour {enemyData.characterName}, annulation du spawn.");
                continue;
            }

            // 🧍 Apparition immédiate de l'ennemi à sa position de combat.
            //     Comme pour l'équipe du joueur, la position est recalculée afin
            //     d'offrir un point d'apparition cohérent avec le type d'unité.
            var spawnPosition = ComputeSpawnPosition(enemyData, spawnPoint);
            var unitGO = Instantiate(enemyData.characterBattleModel, spawnPosition, Quaternion.Euler(0f, 180f, 0f));
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"EnemyUnit_{i}";

            // ✅ Génère l'effet du rayon directement à l'emplacement final.
            if (enemyUnitRay != null)
                Instantiate(enemyUnitRay, spawnPosition, Quaternion.identity);

            var eu = unitGO.GetComponent<CharacterUnit>();
            eu.Initialize(enemyData);
            var allegianceManager = AllegianceManager.Instance;
            if (allegianceManager != null
                && template != null
                && allegianceManager.GetEffectiveAllegiance(template) == AllegianceSide.Enemy
                && eu.IsPlayerControlled)
            {
                eu.ApplyAllegianceOverride(AllegianceSide.Enemy, notifyManagers: false, notifyBattle: false);
            }
            unitsInBattle.Add(eu);
        }
    }
    #endregion

}
