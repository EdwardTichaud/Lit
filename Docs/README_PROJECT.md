# Lit - README projet

## Résumé du jeu

`Lit` est un jeu Unity d'exploration narrative, d'archéologie temporelle et de lecture environnementale. Le joueur explore un château suspendu dans une fracture du temps pour comprendre comment une civilisation a vécu, changé, souffert puis disparu.

La direction actuelle privilégie :

- exploration et observation ;
- registres, lignées et objets transmis ;
- connaissances persistantes comme mémoire de l'enquête ;
- fantômes apaisés par les connaissances découvertes, pas par des réponses libres ;
- transformation des lieux selon les âges ;
- systèmes simples et maintenables par un développeur solo ;
- combat conservé comme tension ponctuelle, pas comme boucle principale.

Les documents de design principaux sont :

- [Lore.md](Lore.md) pour le monde, les courants religieux et la structure narrative ;
- [Gameplay.md](Gameplay.md) pour les piliers jouables ;
- [NarrativeData.md](NarrativeData.md) pour les données de registres, lignées,
  objets transmis et connaissances ;
- [CameraObstruction.md](CameraObstruction.md) pour la caméra, les murs obstruants et la vignette ;
- [TurnBasedCombat.md](TurnBasedCombat.md) pour le combat conservé.

## Version Unity

Le projet est actuellement ouvert avec :

```text
Unity 6000.2.6f2
```

La version exacte est déclarée dans `ProjectSettings/ProjectVersion.txt`.

## Structure générale

```text
Assets/
  Scenes/                 Scènes Unity principales et scènes de test.
  Scripts/                Code C# spécifique au projet.
  ScriptableObjects/      Données de gameplay : items, personnages, audio, visions, etc.
  Prefabs/                Prefabs de gameplay, UI, personnages, château et puzzles.
  Resources/              Assets chargés par Resources.Load.
  Editor/                 Outils Unity Editor propres au projet.
  0 - UnityPackages/      Assets importés, à ne pas modifier sans raison.
  TextMesh Pro/           Package/exemples TextMesh Pro, à ignorer.
  Sketchfab For Unity/    Plugin Sketchfab, à ignorer.
  QuickOutline/           Asset externe probable, à ignorer.
Docs/
  Documentation projet, architecture et guides de maintenance.
Packages/
  Dépendances Unity. Ne pas modifier manuellement dans le cadre normal.
ProjectSettings/
  Réglages Unity. Modifier seulement depuis Unity ou pour une raison explicite.
```

## Scènes principales

Les scènes repérées sont :

- `Assets/Scenes/MainMenu.unity` : menu principal, chargement de sauvegardes et démarrage de session.
- `Assets/Scenes/Maison.unity` : scène de gameplay principale actuelle.
- `Assets/Scenes/StarterMotorTest.unity` : scène de test mouvement/locomotion.
- `Assets/Scenes/SampleScene.unity` : scène Unity de base ou test simple.

Pour une première prise en main, ouvrir `MainMenu.unity`, puis lancer une session qui charge la scène de gameplay.

## Scripts du projet

Les scripts propres au projet sont principalement dans :

- `Assets/Scripts/`
- `Assets/Scripts/Combat/`
- `Assets/Scripts/Menu/`
- `Assets/Scripts/Movement/`
- `Assets/Scripts/Netcode/`
- `Assets/Scripts/Netcode/Persistence/`
- `Assets/Scripts/Temporal/`
- `Assets/Scripts/NarrativeData/`
- `Assets/Editor/`
- `Assets/ScriptableObjects/*/*.cs`

Voir [SCRIPT_INDEX.md](SCRIPT_INDEX.md) pour une liste détaillée.

## Scripts et dossiers à ne pas modifier

Ne pas modifier directement :

- `Packages/`
- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Logs/`
- `UserSettings/`
- `Assets/0 - UnityPackages/`
- `Assets/TextMesh Pro/`
- `Assets/Sketchfab For Unity/`
- `Assets/QuickOutline/`
- `Assets/TutorialInfo/`
- `Assets/PlayerInputs.cs` car il est auto-généré par l'Input System.

## Comment lancer le projet

1. Ouvrir le projet avec Unity `6000.2.6f2`.
2. Laisser Unity réimporter les assets si nécessaire.
3. Ouvrir `Assets/Scenes/MainMenu.unity`.
4. Appuyer sur Play.
5. Utiliser le menu pour créer ou charger une session.

Pour tester directement certains systèmes, utiliser les scènes de test uniquement si leur objectif est clair. `StarterMotorTest.unity` sert surtout à vérifier la locomotion.

## Ce qu'un débutant doit lire en premier

Ordre recommandé :

1. Ce fichier.
2. [CODING_GUIDE_FOR_BEGINNERS.md](CODING_GUIDE_FOR_BEGINNERS.md).
3. [GAME_SYSTEMS.md](GAME_SYSTEMS.md).
4. [ARCHITECTURE.md](ARCHITECTURE.md).
5. [SCRIPT_INDEX.md](SCRIPT_INDEX.md) seulement quand tu cherches un script précis.
6. [Gameplay.md](Gameplay.md) et [Lore.md](Lore.md) pour comprendre la direction créative.

## Règles de maintenance simples

- Ne jamais renommer un script, une classe, un champ public ou un champ `[SerializeField]` sans vérifier les scènes et prefabs.
- Ne pas déplacer un script Unity sans garder son `.meta`.
- Ne pas modifier les assets tiers pour corriger un problème de gameplay.
- Préférer une petite modification testable à une grosse refonte.
- Toujours tester dans Unity après une modification de script.
- Si un système est difficile à comprendre, ajouter une note documentaire avant de refactorer.
