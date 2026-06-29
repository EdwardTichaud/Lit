using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class NewBattleManager
{
    private CharacterUnit forcedNextUnit;
    private readonly Dictionary<CharacterUnit, PrepaidMoveCost> prepaidMoveCosts = new();

    private struct PrepaidMoveCost
    {
        public MusicalMoveSO Move;
        public int HarmonicCost;
    }

    public void ForceNextUnit(CharacterUnit unit)
    {
        if (unit == null || unit.currentHP <= 0)
            return;

        if (!activeCharacterUnits.Contains(unit))
            return;

        forcedNextUnit = unit;
    }

    public bool TryGetMovementRequirement(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move, out float requiredDistance)
    {
        return TryGetMovementRequirement(caster, target, move, out requiredDistance, out _);
    }

    private bool TryGetMovementRequirement(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move,
        out float requiredDistance, out Vector3 targetPosition)
    {
        requiredDistance = 0f;
        targetPosition = Vector3.zero;

        if (caster == null || move == null)
            return false;

        if (!TryResolveMoveTargetPosition(caster, target, move, out targetPosition))
            return false;

        if (IsRepliMove(move))
        {
            requiredDistance = 0f;
            return true;
        }

        float distance = Vector3.Distance(caster.transform.position, targetPosition);
        float maxReach = caster.currentRange + move.castDistance;
        requiredDistance = Mathf.Max(0f, distance - maxReach);
        return true;
    }

    private bool TryResolveApproachDestination(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move,
        out float requiredDistance, out Vector3 destination)
    {
        requiredDistance = 0f;
        destination = Vector3.zero;

        if (!TryGetMovementRequirement(caster, target, move, out requiredDistance, out Vector3 targetPosition))
            return false;

        destination = requiredDistance > 0.01f
            ? Vector3.MoveTowards(caster.transform.position, targetPosition, requiredDistance)
            : caster.transform.position;
        return true;
    }

    private void RegisterPrepaidMoveCost(CharacterUnit caster, MusicalMoveSO move, int harmonicCost)
    {
        if (caster == null || move == null || harmonicCost <= 0)
            return;

        prepaidMoveCosts[caster] = new PrepaidMoveCost
        {
            Move = move,
            HarmonicCost = harmonicCost
        };
    }

    private int ConsumePrepaidMoveCost(CharacterUnit caster, MusicalMoveSO move)
    {
        if (caster == null || move == null)
            return 0;

        if (prepaidMoveCosts.TryGetValue(caster, out PrepaidMoveCost prepaid) && prepaid.Move == move)
        {
            prepaidMoveCosts.Remove(caster);
            return prepaid.HarmonicCost;
        }

        return 0;
    }

    private void ClearPrepaidMoveCost(CharacterUnit caster)
    {
        if (caster != null)
            prepaidMoveCosts.Remove(caster);
    }

    private IEnumerator ApproachToPosition(CharacterUnit caster, Vector3 destination, CharacterUnit lookTarget = null)
    {
        if (caster == null)
            yield break;

        Vector3 start = caster.transform.position;
        if ((destination - start).sqrMagnitude <= 0.0001f)
            yield break;

        const float approachDuration = 1f;
        float distance = Vector3.Distance(start, destination);
        if (distance > 0.01f)
            caster.PlayMovementAnimation(distance);
        float elapsed = 0f;

        while (elapsed < approachDuration)
        {
            float t = Mathf.Clamp01(elapsed / approachDuration);
            caster.transform.position = Vector3.Lerp(start, destination, t);
            UpdateFacingDuringApproach(caster, lookTarget, destination);
            elapsed += Time.deltaTime;
            yield return null;
        }

        caster.transform.position = destination;
        UpdateFacingDuringApproach(caster, lookTarget, destination);
        caster.StopMovementAnimation();
    }

    private static void UpdateFacingDuringApproach(CharacterUnit caster, CharacterUnit lookTarget, Vector3 fallbackDestination)
    {
        if (caster == null)
            return;

        Vector3 targetPosition = lookTarget != null ? lookTarget.transform.position : fallbackDestination;
        Vector3 direction = targetPosition - caster.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        caster.transform.rotation = Quaternion.RotateTowards(
            caster.transform.rotation,
            targetRotation,
            720f * Time.deltaTime);
    }

    #region Gestion des tours de combat
    private CharacterUnit CalculateNextUnit()
    {
        if (forcedNextUnit != null)
        {
            var candidate = forcedNextUnit;
            forcedNextUnit = null;
            if (candidate != null && candidate.currentHP > 0 && activeCharacterUnits.Contains(candidate))
            {
                candidate.currentATB = candidate.ATBMax;
                return candidate;
            }
        }

        while (true)
        {
            foreach (var unit in activeCharacterUnits)
            {
                unit.currentATB += unit.currentInitiative;
                if (unit.currentATB >= ATB_THRESHOLD)
                    return unit;
            }
        }
    }

    public void StartSquadUnitTurn(CharacterUnit characterUnit)
    {
        Debug.Log("Initialisation du menu de combat avec l'unité : " + characterUnit.Data.characterName);

        currentTurnDamage = 0;

        if (currentBattleState == BattleState.None
            || currentBattleState == BattleState.VictoryScreen_Await
            || currentBattleState == BattleState.VictoryScreen_CanContinue
            || currentBattleState == BattleState.GameOverScreen_Await
            || currentBattleState == BattleState.GameOverScreen_CanContinue)
        {
            return;
        }

        if (currentCharacterUnit != null)
            ToggleMenuContainers(false, false, false);

        // Affiche l'interface principale au premier tour du joueur
        BattleTransitionManager.Instance?.ShowBattleUIIfNeeded();
        // S'assure que la timeline devienne visible dès qu'un tour joueur commence
        BattleTimelineUIManager.Instance?.SetVisible(true);

        ChangeCurrentCharacterUnit(characterUnit);

        // Gain automatique d'une harmonique en début de tour
        characterUnit.AddHarmonic(characterUnit.Data.harmonicType);
        // Affiche un popup visuel pour indiquer le gain
        AddHarmonicPopupManager.Instance?.ShowAddHarmonic(characterUnit.transform, 1);

        if (characterUnit.characterType == CharacterType.SquadUnit)
            ChangeBattleState(BattleState.SquadUnit_MainMenu);
        else if (characterUnit.characterType == CharacterType.EnemyUnit)
            ChangeBattleState(BattleState.EnemyUnit_Reflexion);

        SetupCurrentUnitMenus(); // prépare les panels de l’unité
        ShowMainMenu(); // montre le menu principal
        if (characterUnit.IsPlayerControlled)
        {
            SetupCurrentUnitMenus(); // prépare les panels de l’unité
            ShowMainMenu(); // montre le menu principal
        }
        else
        {
            ToggleMenuContainers(false, false, false); // s'assure que les menus sont cachés
        }

        InputsManager.Instance.playerInputs.Battle.Enable();
        OrientAllUnitsTowardClosestOpponent();

        // Affiche l'interface de passage de tour si elle est disponible.
        PassTurnUI.Instance?.Show();
    }

    private IEnumerator ExecuteTurn(CharacterUnit unit)
    {
        if (currentBattleState != BattleState.VictoryScreen_Await && currentBattleState != BattleState.VictoryScreen_CanContinue && currentBattleState != BattleState.GameOverScreen_Await && currentBattleState != BattleState.GameOverScreen_CanContinue)
        {
            yield return WaitWhileBattlePaused();

            if (unit.TryGetComponent<StunnedStatus>(out var stunnedStatus) && stunnedStatus.IsStunned)
            {
                EndTurn(unit, skipIdleAnimation: true);
                yield break;
            }

            if (unit.TryGetComponent<SleepStatus>(out var sleep) && sleep.IsAsleep && unit.Data.gameplayType != GameplayType.Fatigue)
            {
                // En cas de sommeil, on clôt immédiatement le tour de l'unité concernée sans relancer son animation idle
                // afin qu'elle conserve sa pose assoupie. On précise l'unité au gestionnaire pour vider correctement son ATB
                // et éviter que la boucle de tours reste bloquée sur un même combattant.
                EndTurn(unit, skipIdleAnimation: true);
                yield break;
            }
            if (unit.TryGetComponent<FatigueSystem>(out var fatigue) && fatigue.IsAsleep && unit.Data.gameplayType != GameplayType.Fatigue)
            {
                // Même logique que pour le sommeil standard : on force la remise à zéro de l'ATB sans toucher à l'animation,
                // ce qui garantit une transition propre lorsque le système de fatigue impose un repos forcé.
                EndTurn(unit, skipIdleAnimation: true);
                yield break;
            }

            unit.ReduceCooldowns();
            // Réinitialisation des limites par tour
            unit.ResetTurnMoveUsage();
            unit.ResetMovementPoints();
            InventoryManager.Instance?.ResetTurnItemUsage();
            isTurnResolving = true;

            // 1) On stocke l’unité qui jouait juste avant (champ de classe)
            CharacterUnit oldUnit = previousUnit;

            // 2) Mise à jour de l’unité courante
            currentCharacterUnit = unit;
            // Mise à jour de la timeline visuelle via le gestionnaire centralisé
            BattleTimelineUIManager.Instance?.Refresh(unit);

            ChangeBattleState(BattleState.NewTurn);
            PlayTurnStartVoice(unit);

            Debug.Log($"[BattleTurnManager] Tour de {unit.name} (ATB: {unit.currentATB})");
            OrientAllUnitsTowardClosestOpponent();

            // On tente d'abord une transition douce via CrossFade afin d'éviter tout accroc visuel lors du retour à l'idle.
            // Le CharacterUnit encapsule désormais la logique (HasState + fallback Play) pour uniformiser les transitions.
            unit.PlayIdleAnimation();

            // Petite pause avant l'exécution du tour, indépendante du timeScale
            yield return new WaitForSecondsRealtime(0.5f);

            yield return WaitWhileBattlePaused();

            if (unit.IsPlayerControlled)
            {
                StartSquadUnitTurn(unit);
                yield return new WaitUntil(() => !isTurnResolving);
            }
            else
            {
                // 🛰️ Sans cette synchronisation préalable, l'ennemi hérite encore du contexte caméra
                // du joueur précédent. En répercutant immédiatement l'unité en cours dans toute la pile
                // (données locales + gestionnaire global), on garantit que la caméra se recale sur la
                // nouvelle référence avant même que l'IA ne déclenche la moindre action.
                ChangeCurrentCharacterUnit(unit);

                var cameraManager = BattleCameraManager.Instance;
                if (cameraManager != null)
                {
                    // Met à jour l'accès direct "CurrentTurnOwner" et, par extension, "currentCaster".
                    cameraManager.SetTurnOwner(unit);
                    // Aucune cible n'est encore connue : on efface le focus pour laisser la caméra
                    // choisir la bonne orientation lorsqu'elle sera définie.
                    cameraManager.SetCurrentTarget(null);
                }

                yield return EnemyTurnWithQTE(unit);
                EndTurn();
            }

            // 8) On mémorise unit comme précédente pour le prochain tour
            previousUnit = unit;
        }
        else
        {
            yield break;
        }
    }

    /// <summary>
    /// Retire une unité de la timeline visuelle lorsqu'elle quitte le combat.
    /// </summary>
    /// <param name="deadUnit">L'unité vaincue.</param>
    public void RemoveFromTimeline(CharacterUnit deadUnit)
    {
        // Mise à jour de la liste d'unités actives utilisée par la boucle de combat
        activeCharacterUnits.Remove(deadUnit);

        // Le gestionnaire d'UI supprime l'élément graphique correspondant
        BattleTimelineUIManager.Instance?.RemoveFromTimeline(deadUnit);
    }

    public void RemoveFromBattle(CharacterUnit deadUnit)
    {
        if (deadUnit == null)
            return;

        RemoveFromTimeline(deadUnit);
        unitsInBattle.Remove(deadUnit);
    }

    public void RestoreUnitToBattle(CharacterUnit unit)
    {
        if (unit == null || unit.IsPermanentlyDead)
            return;

        if (!unitsInBattle.Contains(unit))
            unitsInBattle.Add(unit);

        if (unit.currentHP > 0f && !activeCharacterUnits.Contains(unit))
            activeCharacterUnits.Add(unit);

        BattleTimelineUIManager.Instance?.AddToTimeline(unit);
    }

    public void OnEnemyDefeated(CharacterUnit enemy)
    {
        rewardItems.AddRange(enemy.lootItems);
        rewardXP += enemy.experienceReward;
        HandleEndOfBattle();
    }

    public void RegisterDamage(CharacterUnit caster, float amount)
    {
        if (caster == null || !caster.IsPlayerControlled)
            return;

        int dmg = Mathf.RoundToInt(amount);
        currentTurnDamage += dmg;

        if (!totalDamageDealt.ContainsKey(caster))
            totalDamageDealt[caster] = 0;
        totalDamageDealt[caster] += dmg;
    }

    public CharacterUnit GetTopDamageDealer()
    {
        if (totalDamageDealt.Count == 0)
            return null;

        int maxDamage = totalDamageDealt.Values.Max();
        var candidates = totalDamageDealt
            .Where(kvp => kvp.Value == maxDamage)
            .Select(kvp => kvp.Key)
            .Where(u => u != null && u.currentHP > 0)
            .ToList();

        if (candidates.Count == 0)
            return null;

        return candidates.OrderBy(u => u.currentHP).First();
    }

    private IEnumerator EnemyTurnWithQTE(CharacterUnit enemy)
    {
        if (enemy == null)
            yield break;

        yield return WaitWhileBattlePaused();

        // Choix de l'action via l'IA centralisée
        var decision = BattleAIStrategy.Decide(enemy, activeCharacterUnits);
        if (decision.Item != null)
        {
            yield return EnemyUseItemRoutine(enemy, decision.Item, decision.Target);
            yield break;
        }

        ChangeBattleState(BattleState.EnemyUnit_PerformingMusicalMove);

        var move = decision.Move ?? enemy.GetRandomMusicalAttack();
        currentMove = move;
        currentItem = null;
        currentMoveIsBasicAttack = false;
        var target = ResolveAIMoveTarget(enemy, decision.Target, move);
        if (target == null)
            target = enemy.SelectTargetFromSquad();

        // Anticipe le comportement d'animation de la cible pour éviter
        // tout doublon pendant la mise en scène.
        RhythmQTEManager.Instance?.PrimeTargetPreparationAnimation(move);

        currentTargetCharacter = target;

        if (move == null || target == null)
        {
            // Sans move ou cible valide on réinitialise immédiatement le drapeau
            // pour ne pas bloquer les prochaines animations défensives.
            RhythmQTEManager.Instance?.PrimeTargetPreparationAnimation(null);
            Debug.LogWarning("[EnemyTurn] Aucune attaque ou cible valide !");
            yield break;
        }

        if (!TryResolveApproachDestination(enemy, target, move, out float requiredDistance, out Vector3 approachDestination))
            yield break;

        if (requiredDistance > 0.01f && !enemy.CanSpendMovementDistance(requiredDistance))
            yield break;

        if (!IsRepliMove(move)
            && move.harmonicCost > 0
            && enemy.GetAvailableHarmonicsForCost(move.consumedHarmonicType) < move.harmonicCost)
            yield break;

        bool alreadyKnown = MusicalCodexManager.Instance != null && MusicalCodexManager.Instance.IsMelodyKnown(move);
        // Affiche le move que l'ennemi prépare
        ActionUIDisplayManager.Instance.DisplayEnemyPreparation(enemy.Data.characterName, alreadyKnown ? move.moveName : null);

        if (requiredDistance > 0.01f)
            enemy.ConsumeMovementDistance(requiredDistance);

        if (!IsRepliMove(move) && move.harmonicCost > 0)
        {
            bool paymentSucceeded = enemy.ConsumeHarmonic(move.consumedHarmonicType, move.harmonicCost);
            if (!paymentSucceeded)
                yield break;
            RegisterPrepaidMoveCost(enemy, move, move.harmonicCost);
        }

        yield return TryResolveInterception(enemy, target, move);
        if (interceptionSucceeded)
            yield break;

        if (requiredDistance > 0.01f)
            yield return ApproachToPosition(enemy, approachDestination, target);

        // Joue un indice sonore associé à l'attaque pour prévenir le joueur
        // et calcule un délai suffisant pour laisser le clip se terminer
        float delay = ENEMY_MOVE_DELAY;
        if (move.warningClip != null)
        {
            // Affiche immédiatement la barre de QTE avec les notes
            RhythmQTEManager.Instance?.PrepareQTEBar(move);
            // Les indices sonores sont joués via la nouvelle source dédiée
            AudioManager.Instance?.PlayWarningClip(move.warningClip);
            // On attend au moins la durée du clip pour conserver la cohérence musicale
            // Utilise la propriété Length du ScriptableObject afin d'éviter les accès
            // directs au composant AudioClip et de centraliser la gestion des durées.
            delay = Mathf.Max(delay, move.warningClip.Length);
        }

        // Laisse un délai pour que le joueur prenne connaissance de l'action
        // On utilise ici un temps réel pour éviter tout blocage si le jeu est en pause
        yield return new WaitForSecondsRealtime(delay);
        yield return WaitWhileBattlePaused();
        yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, enemy, target);

        // Ajoute le move au codex et affiche sa découverte si nécessaire
        if (!alreadyKnown && MusicalCodexManager.Instance != null && MusicalCodexManager.Instance.TryAddNewMelody(move))
        {
            ActionUIDisplayManager.Instance.DisplayMoveDiscovery(move.moveName);
        }
    }

    private IEnumerator EnemyUseItemRoutine(CharacterUnit enemy, ItemData item, CharacterUnit preferredTarget)
    {
        if (enemy == null || item == null)
            yield break;

        yield return WaitWhileBattlePaused();

        var target = ResolveAIItemTarget(enemy, preferredTarget, item) ?? enemy.SelectTargetFromSquad();
        if (target == null)
            yield break;

        currentItem = item;
        currentMove = null;
        currentMoveIsBasicAttack = false;
        currentItemTargetType = item.defaultTargetType;
        currentTargetCharacter = target;

        string itemName = !string.IsNullOrEmpty(item.itemName) ? item.itemName : item.name;
        ActionUIDisplayManager.Instance.DisplayEnemyPreparation(enemy.Data.characterName, itemName);

        if (item.beatPattern != null && item.beatPattern.Count > 0)
            RhythmQTEManager.Instance?.PrepareQTEBar(item.beatPattern);

        yield return new WaitForSecondsRealtime(ENEMY_MOVE_DELAY);
        yield return WaitWhileBattlePaused();

        ChangeBattleState(BattleState.EnemyUnit_Item_Prepare);
        ChangeBattleState(BattleState.EnemyUnit_Item_Use);
        yield return UseItemOnTarget(item, enemy, target, true);
    }

    private CharacterUnit ResolveAIMoveTarget(CharacterUnit enemy, CharacterUnit candidate, MusicalMoveSO move)
    {
        bool allowDeadTargets = move != null && move.usableOnDeathUnits;
        if (candidate != null && (allowDeadTargets || candidate.currentHP > 0))
            return candidate;

        if (move == null)
            return enemy.SelectTargetFromSquad();

        IReadOnlyList<CharacterUnit> sourceUnits = allowDeadTargets ? unitsInBattle : activeCharacterUnits;

        switch (move.targetType)
        {
            case TargetType.Self:
            case TargetType.SpawnPosition:
                return enemy;
            case TargetType.SingleAlly:
            case TargetType.AllAllies:
                return sourceUnits.FirstOrDefault(u =>
                    u != null &&
                    u.characterType == enemy.characterType &&
                    (allowDeadTargets || u.currentHP > 0));
            case TargetType.SingleAllyOrEnemy:
                if (move.defaultTargetType == TargetType.Self)
                    return enemy;
                if (move.defaultTargetType == TargetType.SingleAlly || move.defaultTargetType == TargetType.AllAllies)
                    return sourceUnits.FirstOrDefault(u =>
                        u != null &&
                        u.characterType == enemy.characterType &&
                        (allowDeadTargets || u.currentHP > 0));
                return enemy.SelectTargetFromSquad();
            default:
                return enemy.SelectTargetFromSquad();
        }
    }

    private CharacterUnit ResolveAIItemTarget(CharacterUnit enemy, CharacterUnit candidate, ItemData item)
    {
        bool allowDeadTargets = item != null && item.usableOnDeathUnits;
        if (candidate != null && (allowDeadTargets || candidate.currentHP > 0))
            return candidate;

        if (item == null)
            return null;

        IReadOnlyList<CharacterUnit> sourceUnits = allowDeadTargets ? unitsInBattle : activeCharacterUnits;

        switch (item.defaultTargetType)
        {
            case TargetType.Self:
                return enemy;
            case TargetType.SingleAlly:
            case TargetType.AllAllies:
                return sourceUnits.FirstOrDefault(u =>
                    u != null &&
                    u.characterType == enemy.characterType &&
                    (allowDeadTargets || u.currentHP > 0));
            default:
                return enemy.SelectTargetFromSquad();
        }
    }

    public IEnumerator ExecuteMoveOnTarget(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        Debug.Log($"{caster} exécute le mouvement {move.moveName} sur {target}");
        ToggleMenuContainers(false, false, false);
        // Vérifie les limites d'utilisation avant de lancer le QTE
        if (!caster.CanUseMove(move))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
            yield break;
        }
        if (target != null && target.currentHP <= 0f && !move.usableOnDeathUnits)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Aucune cible valide");
            yield break;
        }
        if (!TryResolveApproachDestination(caster, target, move, out float requiredDistance, out Vector3 approachDestination))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Aucune cible valide");
            yield break;
        }

        if (requiredDistance > 0.01f && !caster.CanSpendMovementDistance(requiredDistance))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Pas assez de points de mouvement");
            yield break;
        }
        if (!HasSpaceForMove(caster, target, move))
        {
            // Affiche un message utilisateur si la position relative est bloquée.
            ActionUIDisplayManager.Instance.DisplayInstruction_TargetPositionOccupied();
            Debug.LogWarning("[ExecuteMoveOnTarget] Pas assez d'espace pour executer le mouvement.");
            yield break;
        }

        if (!IsTargetAltitudeValid(target, move))
        {
            // Message spécifique selon la contrainte de hauteur définie sur le move.
            if (move.altitudeCondition == AltitudeCondition.AirOnly)
                ActionUIDisplayManager.Instance.DisplayInstruction("La cible doit être en l'air sans sol sous elle");
            else if (move.altitudeCondition == AltitudeCondition.GroundOnly)
                ActionUIDisplayManager.Instance.DisplayInstruction("La cible doit être au sol");
            yield break;
        }

        if (move.enterAwake && (caster.IsAwake ||
            caster.GetHarmonicCount(caster.Data.harmonicType) < caster.Data.awakeHarmonicThreshold))
        {
            Debug.LogWarning("[ExecuteMoveOnTarget] Conditions d'Awake non remplies.");
            yield break;
        }

        if (!IsRepliMove(move)
            && move.harmonicCost > 0
            && caster.GetAvailableHarmonicsForCost(move.consumedHarmonicType) < move.harmonicCost)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            yield break;
        }

        if (requiredDistance > 0.01f)
            caster.ConsumeMovementDistance(requiredDistance);

        if (!IsRepliMove(move) && move.harmonicCost > 0)
        {
            bool paymentSucceeded = caster.ConsumeHarmonic(move.consumedHarmonicType, move.harmonicCost);
            if (!paymentSucceeded)
            {
                ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
                yield break;
            }
            RegisterPrepaidMoveCost(caster, move, move.harmonicCost);
        }

        OrientUnitTowardTarget(caster, target);

        yield return TryResolveInterception(caster, target, move);
        if (interceptionSucceeded)
        {
            EndTurn(caster, skipIdleAnimation: true);
            yield break;
        }

        if (requiredDistance > 0.01f)
            yield return ApproachToPosition(caster, approachDestination, target);

        // Lecture d'un avertissement sonore si le mouvement en possède un
        if (move.warningClip != null)
        {
            RhythmQTEManager.Instance?.PrepareQTEBar(move);
            // Les indices sonores sont joués via la nouvelle source dédiée
            AudioManager.Instance?.PlayWarningClip(move.warningClip);
            // On attend la fin du clip pour conserver la cohérence musicale
            // La propriété Length garantit un retour de durée valide même si aucun clip n'est assigné.
            yield return new WaitForSecondsRealtime(move.warningClip.Length);
        }

        yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, caster, target);

        bool moveAppliedEffect = RhythmQTEManager.Instance == null || RhythmQTEManager.Instance.LastMoveAppliedEffect;

        // Ajout du système de rage manuellement
        var rage = caster.GetComponent<RageSystem>();
        if (moveAppliedEffect && rage != null && move.HasEffect(MusicalEffectType.Damage))
        {
            float bonus = rage.CalculateBonusDamage();
            if (bonus > 0)
            {
                target.TakeDamage(bonus, caster.transform);
            }
            if (rage.IsEnraged)
                rage.ConsumeRage();
        }

        var concentration = caster.GetComponent<ConcentrationSystem>();
        if (moveAppliedEffect && concentration != null && move.HasEffect(MusicalEffectType.Damage))
        {
            int baseDamage = move.GetEffectValue(MusicalEffectType.Damage, move.PrimaryEffectValue);
            float bonus = concentration.CalculateBonusDamage(baseDamage + caster.currentPower);
            if (bonus > 0)
            {
                target.TakeDamage(bonus, caster.transform);
            }
        }

        // L'ATB n'est plus remis à zéro afin de permettre l'enchaînement de plusieurs
        // actions (compétence ou objet) dans le même tour tant que le joueur dispose des
        // ressources nécessaires.
        //currentCharacterUnit.currentATB = 0f;
    }

    public IEnumerator UseItemOnTarget(ItemData item, CharacterUnit caster, CharacterUnit target, bool bypassInventory = false)
    {
        if (item == null || caster == null || target == null)
            yield break;

        if (!bypassInventory)
        {
            if (!InventoryManager.Instance.CanUseItem(item))
            {
                ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
                yield break;
            }
        }
        if (target.currentHP <= 0f && !item.usableOnDeathUnits)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Aucune cible valide");
            yield break;
        }

        if (!IsTargetInRange(caster, target, item))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_TargetTooFar();
            yield break;
        }

        OrientUnitTowardTarget(caster, target);

        // Animation ou Timeline d'utilisation
        yield return RhythmQTEManager.Instance.ItemRoutine(item, caster, target);

        bool crit = RhythmQTEManager.Instance.LastItemCritical;
        if (bypassInventory)
        {
            ItemEffectExecutor.ApplyEffect(item, caster, target, crit);
        }
        else
        {
            InventoryManager.Instance.UseItem(item, caster, target, crit);
        }
        if (crit)
        {
            ActionUIDisplayManager.Instance?.DisplayCriticalHit();
            BattleCameraManager.Instance?.TriggerCriticalFeedback(target);
        }

        // Calcule les dégâts totaux infligés par l'objet
        float dmgVal = 0f;
        if (item.HasEffect(ItemEffectType.Damage))
            dmgVal += item.GetTotalEffectValue(ItemEffectType.Damage);
        if (crit && item.useCriticalVariant && item.criticalEffectType == ItemEffectType.Damage)
            dmgVal += item.criticalEffectValue;

        if (dmgVal > 0f)
        {
            RegisterDamage(caster, dmgVal);
        }
        caster.GetComponent<FatigueSystem>()?.OnActionPerformed();
        OrientUnitTowardClosestOpponent(caster);
        // L'utilisation d'un objet ne met plus fin immédiatement au tour
        // On laisse l'ATB inchangé pour permettre l'exécution d'un mouvement ensuite
        yield return null;

        // Retour au menu principal pour choisir une autre action
        ShowMainMenu();
    }

    public IEnumerator UseItemOnTargets(ItemData item, CharacterUnit caster, IReadOnlyList<CharacterUnit> targets,
        bool bypassInventory = false)
    {
        if (item == null || caster == null || targets == null || targets.Count == 0)
            yield break;

        if (!bypassInventory)
        {
            if (!InventoryManager.Instance.CanUseItem(item))
            {
                ActionUIDisplayManager.Instance.DisplayInstruction("Limite d'utilisation atteinte");
                yield break;
            }
        }

        bool allowDeadTargets = item.usableOnDeathUnits;
        CharacterUnit rangeTarget = targets.FirstOrDefault(t => t != null && (allowDeadTargets || t.currentHP > 0));
        if (rangeTarget == null)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction("Aucune cible valide");
            yield break;
        }

        if (!IsTargetInRange(caster, rangeTarget, item))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_TargetTooFar();
            yield break;
        }

        OrientUnitTowardTarget(caster, rangeTarget);

        // Animation ou Timeline d'utilisation (jouée une seule fois).
        yield return RhythmQTEManager.Instance.ItemRoutine(item, caster, rangeTarget);

        bool crit = RhythmQTEManager.Instance.LastItemCritical;

        int appliedTargets = 0;
        foreach (var target in targets)
        {
            if (target == null || (!allowDeadTargets && target.currentHP <= 0))
                continue;

            ItemEffectExecutor.ApplyEffect(item, caster, target, crit);

            appliedTargets++;
        }

        if (crit)
        {
            ActionUIDisplayManager.Instance?.DisplayCriticalHit();
            CharacterUnit feedbackTarget = targets.FirstOrDefault(t => t != null && t.IsPlayerControlled)
                ?? targets.FirstOrDefault(t => t != null);
            BattleCameraManager.Instance?.TriggerCriticalFeedback(feedbackTarget);
        }

        if (!bypassInventory && appliedTargets > 0)
            InventoryManager.Instance.RegisterItemUse(item);

        // Calcule les dégâts totaux infligés par l'objet.
        float dmgVal = 0f;
        if (item.HasEffect(ItemEffectType.Damage))
            dmgVal += item.GetTotalEffectValue(ItemEffectType.Damage);
        if (crit && item.useCriticalVariant && item.criticalEffectType == ItemEffectType.Damage)
            dmgVal += item.criticalEffectValue;

        if (dmgVal > 0f && appliedTargets > 0)
            RegisterDamage(caster, dmgVal * appliedTargets);

        caster.GetComponent<FatigueSystem>()?.OnActionPerformed();
        OrientUnitTowardClosestOpponent(caster);
        yield return null;

        // Retour au menu principal pour choisir une autre action
        ShowMainMenu();
    }

    /// <summary>
    /// Vérifie si l'emplacement relatif requis par le mouvement est libre.
    /// Retourne faux si une autre unité (hors lanceur et cible) occupe déjà la zone.
    /// </summary>
    public bool HasSpaceForMove(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (caster == null || move == null)
            return false;

        if (!TryResolveMoveTargetPosition(caster, target, move, out Vector3 targetPosition))
            return false;

        // Direction à partir de la cible en fonction de la position relative demandée.
        Vector3 direction = Vector3.zero;
        if (move.relativePosition != RelativePosition.On)
        {
            Vector3 referenceDir = caster.transform.position - targetPosition;
            referenceDir.y = 0f;
            if (referenceDir.sqrMagnitude <= 0.0001f)
            {
                referenceDir = target != null ? target.transform.forward : caster.transform.forward;
                referenceDir.y = 0f;
            }

            if (referenceDir.sqrMagnitude > 0.0001f)
                referenceDir = referenceDir.normalized;

            direction = referenceDir;
            switch (move.relativePosition)
            {
                case RelativePosition.Back:
                    direction = -referenceDir;
                    break;
                case RelativePosition.Left:
                    direction = -Vector3.Cross(Vector3.up, referenceDir);
                    break;
                case RelativePosition.Right:
                    direction = Vector3.Cross(Vector3.up, referenceDir);
                    break;
            }
        }

        Vector3 destination = targetPosition + direction * move.castDistance;
        // Recherche de toute unité se trouvant déjà à l'emplacement calculé.
        Collider[] hits = Physics.OverlapSphere(destination, 0.5f);
        foreach (var h in hits)
        {
            CharacterUnit cu = h.GetComponentInParent<CharacterUnit>();
            // On ignore le lanceur et la cible eux-mêmes.
            if (cu != null && cu != caster && cu != target)
                return false;
        }
        return true;
    }

    public bool IsTargetInRange(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (caster == null || move == null)
        {
            Debug.LogWarning("[IsTargetInRange] caster ou move manquant.");
            return false;
        }

        if (!TryResolveMoveTargetPosition(caster, target, move, out Vector3 targetPosition))
        {
            Debug.LogWarning("[IsTargetInRange] Position de cible introuvable.");
            return false;
        }
        float distance = Vector3.Distance(caster.transform.position, targetPosition);
        float maxReach = caster.currentRange + move.castDistance;
        return distance <= maxReach;
    }

    public bool IsTargetInRange(CharacterUnit caster, CharacterUnit target, ItemData item)
    {
        if (caster == null || target == null || item == null)
            return false;

        float distance = Vector3.Distance(caster.transform.position, target.transform.position);
        float maxReach = caster.currentRange + item.castDistance;
        return distance <= maxReach;
    }

    /// <summary>
    ///     Retourne l'attaque basique à utiliser pour une unité donnée en appliquant un fallback
    ///     global si aucune compétence offensive n'est définie dans sa liste personnelle.
    ///     Ce point d'accès centralise la logique afin que les autres systèmes (inputs, statuts...)
    ///     restent cohérents.
    /// </summary>
    /// <param name="unit">Unité pour laquelle déterminer l'attaque basique.</param>
    public MusicalMoveSO ResolveBasicAttackMove(CharacterUnit unit)
    {
        if (unit == null)
            return defaultBasicAttackMove;

        // On privilégie l'attaque basique explicitement configurée dans les données du personnage.
        MusicalMoveSO configuredMove = unit.GetBasicAttack();
        if (configuredMove != null)
            return configuredMove;

        // À défaut, on retombe sur la référence globale (configurable dans l'inspecteur) afin de
        // garantir que chaque unité dispose d'une action minimale pour Presto et l'input dédié.
        return defaultBasicAttackMove;
    }

    /// <summary>
    ///     Vérifie si un <see cref="MusicalMoveSO"/> correspond à l'attaque basique de référence.
    ///     Cette méthode protège le premier slot du SkillsPanel en éliminant toute variante dupliquée
    ///     qui pointerait vers la même action (même nom d'asset ou nom affiché).
    /// </summary>
    /// <param name="candidate">Move évalué.</param>
    /// <param name="basicMove">Move d'attaque basique attendu.</param>
    private bool IsEquivalentToBasicAttack(MusicalMoveSO candidate, MusicalMoveSO basicMove)
    {
        if (candidate == null || basicMove == null)
            return false;

        // 1) Référence exacte : cas standard lorsque le ScriptableObject est partagé.
        if (ReferenceEquals(candidate, basicMove))
            return true;

        // 2) Comparaison sur le nom affiché pour couvrir les variantes clonées conservant le même titre.
        if (!string.IsNullOrEmpty(candidate.moveName) && !string.IsNullOrEmpty(basicMove.moveName)
            && string.Equals(candidate.moveName, basicMove.moveName, StringComparison.OrdinalIgnoreCase))
            return true;

        // 3) Dernier filet : nom Unity de l'asset (utile si le moveName est vide mais que l'asset est dupliqué).
        return string.Equals(candidate.name, basicMove.name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Nombre maximum de compétences musicales affichées par page.
    /// </summary>
    private const int PaginatedSlotMaxCount = 3;
    private const int FirstPaginatedSlotIndex = 2;

    /// <summary>
    ///     Retourne la liste des indices exploitables pour les slots paginés (compétences musicales hors attaque de base,
    ///     hors repli et hors move spécial). On filtre explicitement pour ignorer les deux premiers slots ainsi que le
    ///     dernier qui est réservé à la compétence spéciale, conformément au design du menu et aux attentes des joueurs.
    /// </summary>
    private List<int> BuildPaginatedSkillSlotIndices()
    {
        // On travaille sur une nouvelle liste à chaque appel : la taille reste minuscule (3 entrées maximum) et cela
        // évite de gérer manuellement des références potentiellement partagées entre plusieurs appels.
        List<int> indices = new List<int>(PaginatedSlotMaxCount);

        if (currentSkillsMenuSlots == null || currentSkillsMenuSlots.Count == 0)
            return indices;

        // Le dernier index correspond toujours au slot dédié au mouvement spécial. On le garde à portée pour filtrer
        // les indices supérieurs ou égaux à cette valeur afin de préserver l'indépendance de ce slot.
        int specialSlotIndex = currentSkillsMenuSlots.Count - 1;

        for (int slotIndex = FirstPaginatedSlotIndex; slotIndex < specialSlotIndex; slotIndex++)
        {
            // a) Les indices négatifs ou au-delà de la taille réelle sont ignorés pour éviter toute exception de type
            //    IndexOutOfRangeException si la hiérarchie UI a été modifiée sans répercussion dans le code.
            if (slotIndex < 0 || slotIndex >= currentSkillsMenuSlots.Count)
                continue;

            // b) On exclut volontairement le dernier slot (mouvement spécial) pour que Left/Right Shoulder ne
            //    paginent jamais ce bouton et laissent l'affichage immuable, comme demandé.
            if (slotIndex >= specialSlotIndex)
                continue;

            indices.Add(slotIndex);
            if (indices.Count >= PaginatedSlotMaxCount)
                break;
        }

        return indices;
    }

    /// <summary>
    ///     Calcule le nombre de slots réellement disponibles pour les compétences paginées.
    ///     Les deux premiers slots (attaque de base + repli) et le dernier (move spécial) sont exclus pour
    ///     empêcher toute réécriture accidentelle des emplacements réservés.
    /// </summary>
    public int GetPaginatedSkillSlotCount()
    {
        // Plutôt qu'un simple calcul « Count - 3 », on reconstruit explicitement la liste d'indices autorisés afin de
        // verrouiller les slots réellement exploités (hors attaque basique, repli et move spécial).
        return BuildPaginatedSkillSlotIndices().Count;
    }

    /// <summary>
    /// Vérifie si la hauteur actuelle de la cible correspond aux exigences du mouvement.
    /// </summary>
    public bool IsTargetAltitudeValid(CharacterUnit target, MusicalMoveSO move)
    {
        if (target == null || move == null)
            return false;

        switch (move.altitudeCondition)
        {
            case AltitudeCondition.AirOnly:
                // Attaque réservée aux unités sans aucun sol sous elles.
                // Qu'une unité soit terrestre ou aérienne, la présence d'un support
                // en contrebas la protège de ce type d'assaut venu du ciel.
                return !target.HasGroundBelow();
            case AltitudeCondition.GroundOnly:
                // Attaque réservée aux unités terrestres posées sur un support.
                // Cependant, si une unité survole un sol, elle peut aussi être touchée
                // par les attaques terrestres qui résonnent à travers la scène,
                // tandis qu'elle devient intouchable par les attaques aériennes.
                return (!target.IsAirUnit && target.IsGrounded) || target.HasGroundBelow();
            default:
                // Aucune restriction : mouvement utilisable dans toutes les configurations.
                return true;
        }
    }

    private IEnumerator TryResolveInterception(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        interceptionSucceeded = false;

        if (!CanAttemptInterception(caster, move) || target == null)
            yield break;

        var interceptor = FindBestInterceptor(caster, target);
        if (interceptor == null)
            yield break;

        float interceptorChance = ResolveInterceptionChance(interceptor, caster);
        float interceptableChance = ResolveInterceptionChance(caster, interceptor);
        bool success = ResolveInterceptionOutcome(interceptor, interceptorChance, interceptableChance);

        if (success)
        {
            interceptionSucceeded = true;
            yield return InterceptRoutine(interceptor, caster);
        }

        bool allyWasInterceptor = interceptor.IsPlayerControlled;
        CharacterUnit popupTarget = allyWasInterceptor ? interceptor : caster;
        if (popupTarget == null)
            popupTarget = interceptor ?? caster;
        ActionUIDisplayManager.Instance?.DisplayInterceptionOutcome(popupTarget != null ? popupTarget.transform : null,
            success, allyWasInterceptor);
    }

    private bool CanAttemptInterception(CharacterUnit caster, MusicalMoveSO move)
    {
        if (caster == null || move == null)
            return false;

        if (caster.Data == null)
            return false;

        return move.interceptable && caster.Data.interceptable && !caster.isInterceptionImmune;
    }

    private CharacterUnit FindBestInterceptor(CharacterUnit caster, CharacterUnit target)
    {
        if (caster == null)
            return null;

        CharacterUnit best = null;
        float bestChance = -1f;
        foreach (var unit in activeCharacterUnits)
        {
            if (unit == null || unit == caster || unit == target)
                continue;
            if (unit.IsPlayerControlled == caster.IsPlayerControlled)
                continue;

            float range = unit.currentInterceptionRange;
            if (range <= 0f)
                continue;

            if (Vector3.Distance(unit.transform.position, caster.transform.position) > range)
                continue;

            var conc = unit.GetComponent<ConcentrationSystem>();
            if (conc != null && conc.IsFull)
                return unit;

            float chance = ResolveInterceptionChance(unit, caster);
            if (chance > bestChance)
            {
                bestChance = chance;
                best = unit;
            }
        }
        return best;
    }

    private float ResolveInterceptionChance(CharacterUnit interceptor, CharacterUnit interceptable)
    {
        if (interceptor == null)
            return 0f;

        float interceptorReflex = Mathf.Max(0f, interceptor.currentReflex);
        float interceptableReflex = interceptable != null ? Mathf.Max(0f, interceptable.currentReflex) : 0f;
        float chance = interceptorReflex / (interceptorReflex + interceptableReflex + 1f);

        if (interceptor.currentInterceptionChance > 0f)
            chance = Mathf.Clamp01(chance + interceptor.currentInterceptionChance);

        return chance;
    }

    private bool ResolveInterceptionOutcome(CharacterUnit interceptor, float interceptorChance, float interceptableChance)
    {
        var conc = interceptor != null ? interceptor.GetComponent<ConcentrationSystem>() : null;
        if (conc != null && conc.IsFull)
            return true;

        if (interceptorChance <= 0f && interceptableChance <= 0f)
            return false;

        float total = interceptorChance + interceptableChance;
        float roll = UnityEngine.Random.value * total;
        return roll <= interceptorChance;
    }

    private IEnumerator InterceptRoutine(CharacterUnit interceptor, CharacterUnit caster)
    {
        if (interceptor == null) yield break;

        caster?.PlayInterceptedAnimation();
        caster?.PlayInterceptedSound();
        caster?.ConsumeAllMovementPoints();
        caster?.ReduceHarmonicsByHalf();
        ClearPrepaidMoveCost(caster);
        interceptor?.PlayInterceptionAnimation();
        // Son joué par l'unité qui intercepte
        interceptor?.PlayInterceptionSound();

        var move = interceptor.GetRandomMusicalAttack();
        if (move != null)
        {
            ActionUIDisplayManager.Instance.DisplayActionMessage(interceptor.Data.characterName, move.moveName, caster.Data.characterName);
            yield return RhythmQTEManager.Instance.MusicalMoveRoutine(move, interceptor, caster);
            if (move.notes == null || move.notes.Count == 0)
                MusicalMoveExecutor.ApplyEffect(move, interceptor, caster);
        }
    }

    public void EndTurn(CharacterUnit forcedUnit = null, bool skipIdleAnimation = false)
    {
        // Détermine l'unité qui termine effectivement son tour :
        //  * par défaut, on conserve l'unité actuellement enregistrée ;
        //  * certains raccourcis (sommeil, fatigue...) fournissent explicitement l'unité à traiter
        //    pour remettre son ATB à zéro et éviter tout blocage de la boucle de tours.
        CharacterUnit endingUnit = forcedUnit ?? currentCharacterUnit;

        // Actualise la référence interne pour garder une trace cohérente du dernier combattant résolu.
        currentCharacterUnit = endingUnit;

        if (endingUnit != null)
        {
            Debug.Log($"[BattleTurnManager] Fin du tour de {endingUnit.name}");
            endingUnit.currentATB = 0f;

            if (endingUnit.interceptionImmunityTurns > 0)
            {
                endingUnit.interceptionImmunityTurns--;
                if (endingUnit.interceptionImmunityTurns <= 0)
                    endingUnit.isInterceptionImmune = false;
            }

            // ⏳ Gère la décrémentation des états temporaires (ancrage, suspension…)
            //     directement sur l'unité afin que le BattleManager n'ait pas à
            //     connaître les détails d'implémentation.
            endingUnit.ProcessEndOfTurnStatuses();

            if (endingUnit.IsPlayerControlled && currentTurnDamage > maxTurnDamage)
            {
                maxTurnDamage = currentTurnDamage;
                mvpUnit = endingUnit;
            }

            // On évite de forcer l'animation d'attente lorsque l'unité est censée dormir ou rester figée.
            if (!skipIdleAnimation)
            {
                // Idem que pour le début de tour : on délègue au CharacterUnit pour garantir un fondu systématique.
                endingUnit.PlayIdleAnimation();
            }

            PlayTurnEndVoice(endingUnit);
        }

        ChangeBattleState(BattleState.EndTurn);
        // Cache tous les menus à la fin du tour
        ToggleMenuContainers(false, false, false);
        // Réinitialise la timeline visuelle (ordre + surbrillance)
        BattleTimelineUIManager.Instance?.Refresh(null);
        isTurnResolving = false;
        // Déclenche les attaques automatiques liées à Presto avant l'évaluation de fin de combat.
        PrestoForcedAttackSystem.HandleTurnEnded(endingUnit);
        HandleEndOfBattle();

        // Cache la jauge si elle existe pour éviter les références invalides.
        PassTurnUI.Instance?.Hide(); // Bouclage

        // Lance immédiatement les timelines conditionnelles si elles ont été mises en file pendant le tour.
        TryStartPendingTimelines();
    }

    public void AfterMusicalMove(MusicalMoveSO move, CharacterUnit caster, bool wasCritical)
    {
        if (lastBattleOutcome != BattleOutcome.None
            || victorySequenceInProgress
            || currentBattleState == BattleState.VictoryScreen_Await
            || currentBattleState == BattleState.VictoryScreen_CanContinue
            || currentBattleState == BattleState.GameOverScreen_Await
            || currentBattleState == BattleState.GameOverScreen_CanContinue)
        {
            return;
        }

        // Affiche un message si toutes les notes du QTE ont été réussies
        if (wasCritical)
            ActionUIDisplayManager.Instance?.DisplayCriticalHit();

        // On capture l'état de l'attaque basique avant toute modification pour pouvoir
        // décider en fin de méthode si le tour doit se terminer immédiatement.
        bool resolveAsBasicAttack = currentMoveIsBasicAttack;
        currentMoveIsBasicAttack = false; // Reset systématique pour éviter les effets de bord.

        if (caster != null)
        {
            bool isRepli = IsRepliMove(move);
            int cost = isRepli ? 0 : move.harmonicCost;
            int generation = isRepli ? 0 : move.harmonicGeneration;

            if (!isRepli && wasCritical && move.useCriticalVariant)
            {
                // Ajoute les valeurs spécifiées pour le coup critique
                cost += move.criticalHarmonicCost;
                generation += move.criticalHarmonicGeneration;
            }

            int prepaidCost = ConsumePrepaidMoveCost(caster, move);
            if (prepaidCost > 0)
                cost = Mathf.Max(0, cost - prepaidCost);

            HarmonicType costType = move.consumedHarmonicType;
            HarmonicType generationType = move.generatedHarmonicType;

            if (cost > 0)
            {
                bool paymentSucceeded = caster.ConsumeHarmonic(costType, cost);
                if (!paymentSucceeded)
                    Debug.LogWarning($"[AfterMusicalMove] {caster.Data.characterName} n'avait pas assez d'harmoniques {costType} pour {move.moveName}.");
            }
            if (generation > 0)
                caster.AddHarmonic(generationType, generation);
            caster.SetMoveCooldown(move);
            caster.RegisterMoveUse(move);

            // Activation du mode Awake si le move le permet
            if (move.enterAwake && !caster.IsAwake &&
                caster.GetHarmonicCount(caster.Data.harmonicType) >= caster.Data.awakeHarmonicThreshold)
            {
                caster.EnterAwakeState();
            }

            if (isRepli)
            {
                EndTurn();
                return;
            }

            // Si l'unité n'a plus d'harmonique, son tour se termine immédiatement
            if (caster.GetHarmonicCount(caster.Data.harmonicType) <= 0)
            {
                EndTurn();
                return;
            }
        }

        if (resolveAsBasicAttack)
        {
            // L'attaque basique met traditionnellement fin au tour : on conserve ce comportement
            // pour ne pas bouleverser l'équilibrage ni les attentes des joueurs.
            EndTurn();
            return;
        }

        if (!caster.IsPlayerControlled)
        {
            EndTurn();
            return;
        }

        //
        // Vérifie si au moins une compétence reste utilisable pour le lanceur.
        // On prend en compte les moves standards ainsi que le move spécial
        // éventuel. On ignore ceux en cooldown pour éviter de terminer
        // prématurément le tour.

        IEnumerable<MusicalMoveSO> availableMoves = caster.Data.musicalAttacks;
        if (caster.Data.specialMusicalMove != null)
            availableMoves = availableMoves.Append(caster.Data.specialMusicalMove);

        bool hasSkill = availableMoves.Any(m =>
            (!m.onlyAwake || caster.IsAwake) &&
            (!m.enterAwake || !caster.IsAwake) &&
            caster.GetAvailableHarmonicsForCost(m.consumedHarmonicType) >= m.harmonicCost &&
            (!m.enterAwake || caster.GetHarmonicCount(caster.Data.harmonicType) >= caster.Data.awakeHarmonicThreshold) &&
            !caster.IsMoveOnCooldown(m));
        bool hasItem = InventoryManager.Instance.GetUsableItems(caster).Count > 0;

        if (!hasSkill && !hasItem)
            EndTurn();
        else
            ShowMainMenu();
    }

    public IEnumerator ShowMoveInfoAndHandleSelection(MusicalMoveSO move)
    {
        string costInfo = move.harmonicCost > 0
            ? $"{move.harmonicCost} harmonique(s) de {move.consumedHarmonicType}"
            : "0 harmonique";
        string gainInfo = move.harmonicGeneration > 0
            ? $"{move.harmonicGeneration} harmonique(s) de {move.generatedHarmonicType}"
            : "aucune harmonie";
        string message = $"{move.description}\nCoût : {costInfo}\nGénère : {gainInfo}";
        InfoBoxManager.Instance.OpenInfoBox(move.moveName, message, move.moveIcon);
        while (!InfoBoxManager.Instance.choix.HasValue)
            yield return null;

        if (InfoBoxManager.Instance.choix.Value)
        {
            ToggleMenuContainers(false, false, false);
            HandleTargetSelection(move);
            // La Timeline de préparation prend en charge l'animation de ciblage
        }
        else
        {
            OpenSkillsMenu();
        }
    }

    public IEnumerator ShowItemInfoAndHandleSelection(ItemData item)
    {
        string message = item.description;
        InfoBoxManager.Instance.OpenInfoBox(item.itemName, message, item.itemIcon);
        while (!InfoBoxManager.Instance.choix.HasValue)
            yield return null;

        if (InfoBoxManager.Instance.choix.Value)
        {
            ToggleMenuContainers(false, false, false);
            HandleTargetSelection(item);
            // L'animation de ciblage est désormais gérée par la Timeline de préparation
        }
        else
        {
            OpenItemMenu();
        }
    }
    #endregion
}
