# Lit — Opérations, QA et performance

Ce document rassemble les procédures de validation, la QA late join, le plan de
performance et les workflows de maintenance spécialisés.

## Validation générale

### Compilation

Version Unity : `6000.4.9f1`.

Exemple batchmode depuis Windows :

```bash
timeout 240s "/mnt/c/Program Files/Unity/Hub/Editor/6000.4.9f1/Editor/Unity.exe" \
  -batchmode -quit \
  -projectPath "C:\Users\pierr\git\Lit" \
  -logFile -
```

Ne pas lancer une seconde instance sur le même projet si Unity est déjà ouvert.

### Tests minimaux par système

| Système | Vérifications |
| --- | --- |
| Mouvement | marche, course, saut, échelle, changement de personnage |
| Interaction | porte, levier, Flame, item, readable |
| Inventaire | ramassage, usage, drop, lecture |
| UI | ouverture/fermeture, clavier, souris, manette |
| Temps | Flame ancien, année, visibilité, shader |
| Fantôme | proximité, connaissance, apaisement, sauvegarde |
| Sauvegarde | nouvelle partie, sauvegarde, sortie, rechargement |
| Netcode | host, client, interaction serveur, client tardif |
| Combat | aggro, tours, item, victoire, défaite, retour |
| Caméra | obstruction, rotation, zoom, caméra fixe, combat |

## QA du monde persistant et late join

Filtrer la Console sur `[PersistentWorld]`.

Critères de passage :

- le client reste bloqué jusqu'à la reconstruction complète ;
- les phases apparaissent dans l'ordre ;
- `snapshot apply summary success=true` est présent ;
- aucune collision d'ID, erreur de provider ou duplication n'apparaît.

Ordre attendu :

```text
client connected
snapshot requested
snapshot sent
snapshot received
resolve scene objects
spawn missing runtime objects
remove invalid objects
apply transforms and active states
apply gameplay state
finalize references
release gameplay
```

### Scénarios obligatoires

#### Session fraîche puis client tardif

1. Le host modifie au moins deux systèmes persistants.
2. Un client rejoint ensuite.
3. Le client reste masqué jusqu'à `release gameplay`.
4. L'état modifié est visible dès sa libération.

#### Plusieurs clients échelonnés

1. Le host modifie le monde.
2. Le client A rejoint.
3. Le host modifie encore le monde.
4. Le client B rejoint.
5. A conserve l'état live et B reçoit le snapshot le plus récent.

#### Conteneur partiellement vidé

- Le client voit le contenu restant, pas le contenu par défaut.
- Log attendu : `validation scenario=container_partial_loot success=true`.

#### Puzzle partiellement résolu

- Les leviers et l'état de résolution correspondent au host.
- Log attendu : `validation scenario=puzzle_progress success=true`.

#### Flame allumé

- État du Flame, année, roots de période et volumes correspondent.
- Logs attendus :
  `validation scenario=flame_world_rules success=true` et
  `validation scenario=world_rules_extended success=true`.

#### Compatibilité Building legacy

- aucun panel ou placement de construction n'est accessible ;
- aucun snapshot `building:` n'est reconstruit ;
- les anciennes données restent présentes après une nouvelle sauvegarde ;
- réactiver uniquement via `LegacyBuildingSystemSettings.systemEnabled` pour
  une campagne de migration dédiée.

#### Connaissance ou trésor découvert

- Déjà débloqué à l'arrivée du client.
- Log : `validation scenario=treasure_found success=true`.

#### Loot jeté

- Une seule instance avec le bon payload.
- Log : `validation scenario=dropped_loot success=true`.

#### Passage secret détecté

- `TrouEtroit` déjà dans l'état détecté.
- Log : `validation scenario=interactable_custom_state success=true`.

#### Sauvegarde, rechargement du host, puis join

1. Modifier loot, puzzle, Flame, connaissance et objet jeté.
2. Sauvegarder et quitter.
3. Recharger comme host.
4. Faire rejoindre un client.
5. Vérifier host et client contre le même état restauré.

### Signatures d'échec

- `persistent object missing persistent ID`
- `persistent ID collision`
- `snapshot resolve failed`
- `snapshot resolve kind mismatch`
- `snapshot resolve prefab mismatch`
- `provider payload invalid`
- `provider state application failed`
- `restore order issue`
- `runtime reconstruction failed`
- `late-join snapshot ready ack transfer mismatch`
- `late-join snapshot transfer still pending`
- `late-join snapshot transfer replaced`
- `duplicate dropped-loot reconstruction avoided`

Causes fréquentes :

- acknowledgement tardif après remplacement d'un transfert ;
- spawn runtime contournant l'allocateur persistant ;
- prefab absent du registre de session ;
- dépendance restaurée dans la mauvaise phase ;
- objet reconstruit puis recréé par une autre logique ;
- join accepté avant la fin du chargement du host.

## Performance

### État actuel vérifié

- architecture d'optimisation de visibilité opt-in ;
- cache des renderers compatibles `_AgeAmount` dans `AgeManager` ;
- locomotion principale portée par UCC et les bridges
  `Assets/Scripts/OpsiveIntegration/` ;
- ancienne caméra CRPG isolée sous `Assets/CameraSystem_Legacy/` ;
- outil `Tools/Lit/Performance/Print Build Dependency Audit`.

Les anciennes affirmations de suppression d'assets de démonstration ou de scènes
de récupération ont été retirées : elles doivent être vérifiées sur l'état réel
du dépôt avant toute décision.

### Risques CPU

- recherches globales dans certains managers ;
- mises à jour fréquentes de UI monde ;
- caméra legacy et scripts XRay encore compilés sous
  `Assets/CameraSystem_Legacy/` ;
- profils de visibilité trop agressifs ou trop fréquents.

Actions :

- créer des registres pour Flames, interactables et personnages ;
- réserver les recherches globales à l'initialisation, au save/load ou à
  l'Editor ;
- mettre à jour la UI monde seulement lorsqu'elle est visible ;
- préconfigurer les cibles d'outline sur les prefabs.

### Risques GPU

- ombres HDRP temps réel ;
- nombreux point lights, flames et Flames ;
- props avec beaucoup de renderers/materials ;
- transparence et bloom des fantômes ;
- passes outline/XRay ;
- textures de displacement ou EXR inutilisées.

Actions :

- ombres 256/512 pour les lumières secondaires ;
- contact shadows seulement sur les lumières importantes ;
- baking ou mixed pour le statique ;
- ranges courts et masks précis ;
- profile du nombre de shadow casters et set pass calls.

### Mémoire, import et build

Ordres de grandeur observés lors de l'audit initial :

- `Assets/0 - UnityPackages` : environ 32 Go ;
- `Assets/Audio` : environ 1,7 Go ;
- `Assets/Lucian_CC5_Embed` : environ 887 Mo.

Ces chiffres peuvent évoluer. Refaire un audit avant toute suppression.

Workflow :

1. lancer le Build Dependency Audit ;
2. identifier les racines avec `buildDeps=0` ;
3. vérifier manuellement les GUID et chargements dynamiques ;
4. déplacer ou supprimer par petits lots ;
5. compiler Unity après chaque lot ;
6. comparer la taille du build.

### Import settings

- textures monde : mipmaps et compression ;
- UI : pas de mipmaps si inutile ;
- tailles maximales de plateforme raisonnables ;
- read/write désactivé sur meshes statiques quand sûr ;
- compression ou streaming des pistes audio longues ;
- suppression des displacement maps non utilisées.

### Baseline Profiler

Profiler `Maison` en Development Build, Deep Profiling désactivé. Relever :

- frame time ;
- main thread ;
- render thread ;
- allocations GC ;
- batches et set pass calls ;
- shadow casters ;
- lumières realtime ;
- mémoire texture.

## Tests spécialisés

### Caméra

- passer derrière un mur sans changement de distance ;
- confirmer fade ou fallback ;
- confirmer la restauration ;
- tester plusieurs murs, zoom et caméra fixe.

### Visibilité

- vérifier collisions, ombres et interactions des objets culled ;
- confirmer que les obstacles caméra protégés restent disponibles ;
- tourner rapidement la caméra pour détecter le pop-in ;
- comparer Profiler avant/après.

### Neige

Si les scintillements ou empreintes manquent, vérifier :

1. collider ;
2. `groundMask` ;
3. normale du sol ;
4. shader avec `_SnowAmount` ;
5. valeur de neige ;
6. personnage retourné par `LocalPlayerUtils.GetControlledCharacter()`.

### Combat

1. créer et baker un `Enemy` ;
2. entrer dans le trigger ;
3. vérifier téléportation et premier tour ennemi ;
4. attaquer, passer et utiliser un item ;
5. vérifier le timeout à 30 secondes ;
6. tester une prière d'idole depuis un autre joueur ;
7. valider victoire, défaite et retour.

### Fantôme

- apparition de proximité ;
- réaction indisponible sans connaissance ;
- réaction disponible avec connaissance ;
- effet `triggerEffectIds` ;
- `PacifiedMemoryReward` accorde +3 une seule fois à l'apaisement ;
- état apaisé après sauvegarde/chargement ;
- état correct pour un client tardif.

### Charges de Munin

1. Partir de 10 charges.
2. Allumer une `Flame` à coût 1 : vérifier 9/10.
3. L'éteindre : vérifier que la valeur reste 9/10.
4. Allumer une grande `Flame` ou une `AncientFlame` à coût 2 : vérifier 7/10.
5. L'éteindre : vérifier que la valeur reste 7/10 et que les conséquences de
   noirceur/influence continuent.
6. Utiliser `MemoryShard.prefab` : vérifier +1 et sa consommation persistante.
7. Apaiser un fantôme de `Maison` : vérifier +3 une seule fois.
8. Utiliser `VigilAltar_Hub_Safeguard` : vérifier la recharge complète, puis le
   cooldown de 60 secondes.
9. Sauvegarder/recharger : vérifier les charges du personnage et les récompenses
   uniques déjà consommées.
10. Tester host et client : chaque joueur doit voir son compteur local correct ;
    vérifier qu'une source unique ne peut pas être exploitée deux fois.

## Intégration de Lucian CC5

### Références

- source : `Assets/Lucian_CC5_Embed`
- prefab source :
  `Assets/Lucian_CC5_Embed/Prefabs/Lucian_CC5_Character_Model.prefab`
- prefab HQ :
  `Assets/Lucian_CC5_Embed/Prefabs/Lucian_CC5_Unity_HQ.prefab`
- matériaux HQ :
  `Assets/Lucian_CC5_Embed/Materials/Lucian_Unity_HQ`
- scène de test :
  `Assets/Scenes/CharacterTests/Lucian_RenderTest.unity`
- outil :
  `Assets/Editor/LucianCC5HqIntegration.cs`

Le prefab joueur doit utiliser les variantes `_Unity_HQ`, pas les matériaux
Reallusion originaux.

### Matériaux et textures

Les originaux ne sont pas écrasés. Les variantes HDRP sont dupliquées.

- normal maps : type Normal Map ;
- mask/metallic/AO/data maps : sRGB désactivé ;
- color maps : sRGB activé ;
- hair/lash/transparency : alpha conservé et coverage des mipmaps ;
- max standalone : 4096 ;
- compression haute qualité.

Certains shaders Reallusion sont absents. Le fallback HDRP/Lit évite les matériaux
roses, avec alpha clipping pour cheveux et cils.

Rapport :

`Assets/Lucian_CC5_Embed/Lucian_CC5_Unity_HQ_Report.txt`

### Rig

- Humanoid ;
- avatar créé depuis le modèle ;
- blendshapes actifs ;
- quatre bones par vertex ;
- optimisation des bones active.

Ne pas passer en Generic ni désactiver les blendshapes sans raison explicite.

### Workflow de mise à jour

1. Exporter la nouvelle version CC5.
2. Conserver les noms de textures autant que possible.
3. Laisser Unity terminer l'import.
4. Lancer `Tools/Lit/Characters/Lucian/Build CC5 HQ Integration`.
5. Ouvrir `Lucian_RenderTest.unity`.
6. Vérifier peau, cheveux, yeux, vêtements et shaders.
7. Vérifier avatar Humanoid et blendshapes si le FBX a changé.

Limites connues :

- certaines maps de thickness/SSS référencées sont absentes ;
- Reallusion Auto Setup n'était pas installé lors de l'intégration ;
- ouvrir les assets une fois dans Unity pour faire remonter les avertissements
  de compatibilité shader.

## Maintenance du dépôt

- Ne supprimer un script Unity qu'après recherche du GUID, du type et des usages
  runtime/editor.
- Ne pas supprimer automatiquement les scripts avec
  `[RuntimeInitializeOnLoadMethod]`, `[InitializeOnLoad]`, `[MenuItem]` ou
  `[CreateAssetMenu]`.
- Ne pas confondre purification et refactor de responsabilités.
- Ne pas purifier les dossiers vendeurs sans revue manuelle.
- Compiler après chaque lot de suppressions.

## Validation des ReadableLore

Les ReadableLore consacrés aux Chanteurs, à la statue, au siège, aux assaillants
et aux traces bleues font partie du canon. Ils ne doivent pas être dépréciés pour
ces raisons.

Lors d'une réécriture :

1. préserver les `itemId` et `readableContentId` déjà référencés ;
2. conserver les trois courants officiels ;
3. traiter les textes bleus comme des traces anonymes de doute, pas comme une
   quatrième religion publique ;
4. vérifier le contenu contre `Docs/Design/Lore.md`.
