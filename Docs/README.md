# Lit — Documentation

Ce dossier contient la documentation canonique du projet. Il est volontairement
organisé autour de quelques documents de référence plutôt que d'une accumulation
de notes spécialisées.

## Sources de vérité

En cas de contradiction, utiliser cet ordre de priorité :

1. [Design/Lore.md](Design/Lore.md) pour les faits du monde et les éléments
   narratifs établis.
2. [Design/GameDesign.md](Design/GameDesign.md) pour la boucle de jeu, les
   priorités de production et les données narratives.
3. [Technical/Architecture.md](Technical/Architecture.md) pour le fonctionnement
   actuel du projet et l'intégration des systèmes.
4. [Technical/Operations.md](Technical/Operations.md) pour les validations,
   performances et procédures de maintenance.
5. [Technical/ScriptIndex.md](Technical/ScriptIndex.md) pour retrouver un script.

Une idée absente du lore canonique, ou explicitement placée dans « Questions
ouvertes », ne doit pas être présentée comme un fait établi dans un nouveau
contenu.

## Structure

```text
Docs/
  README.md
  Design/
    Lore.md
    GameDesign.md
  Technical/
    Architecture.md
    Operations.md
    ScriptIndex.md
  Legal/
    Credits.md
    Opsive_UCC_Bill.png
```

Les anciens fichiers thématiques ont été fusionnés dans ces documents. Les
sections détaillées restent accessibles depuis leur table des matières.

## Résumé du projet

`Lit` est un jeu Unity d'exploration narrative et d'archéologie temporelle. Des
Explorateurs étudient les vestiges d'un peuple qui a vécu enfermé pendant 666 ans
dans un château conçu pour préserver certaines lignées jusqu'à un rituel.

La direction actuelle privilégie :

- l'observation et la comparaison des traces ;
- les familles, registres, lignées et objets transmis ;
- les connaissances persistantes comme mémoire de l'enquête ;
- les fantômes apaisés par des connaissances retrouvées ;
- des systèmes simples et maintenables par un développeur solo ;
- un combat rare, utilisé comme tension ponctuelle.

## Environnement

- Version Unity canonique : `6000.4.9f1`.
- Source : `ProjectSettings/ProjectVersion.txt`.
- Pipeline : HDRP.
- Scène de menu : `Assets/Scenes/MainMenu.unity`.
- Scène de gameplay principale : `Assets/Scenes/Maison.unity`.
- Scène de locomotion/test : `Assets/Scenes/MovementLab.unity`.

Pour lancer le projet :

1. Ouvrir le projet avec Unity `6000.4.9f1`.
2. Laisser Unity terminer les imports.
3. Ouvrir `Assets/Scenes/MainMenu.unity`.
4. Entrer en Play Mode et créer ou charger une session.

## Structure du dépôt

```text
Assets/
  Scenes/                 Scènes principales et scènes de test.
  Scripts/                Code C# du projet.
  ScriptableObjects/      Données de gameplay et de narration.
  Prefabs/                Prefabs de gameplay, UI et environnement.
  Resources/              Assets chargés par Resources.Load.
  Editor/                 Outils Unity Editor propres au projet.
  0 - UnityPackages/      Assets importés.
Packages/                 Dépendances Unity.
ProjectSettings/          Configuration Unity.
Docs/                     Documentation du projet.
```

Ne pas modifier directement les dossiers générés ou tiers :

- `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/` ;
- `Packages/`, sauf maintenance explicite d'une dépendance ;
- `Assets/0 - UnityPackages/`, `Assets/TextMesh Pro/`,
  `Assets/Sketchfab For Unity/` ;
- `Assets/PlayerInputs.cs`, généré depuis `Assets/PlayerInputs.inputactions`.

## Guide de modification

Avant de modifier un système :

1. Décrire le besoin en une phrase.
2. Chercher le système existant avec `rg`.
3. Identifier le `ScriptableObject` qui porte les données, le `MonoBehaviour`
   attaché en scène et le manager qui orchestre le comportement.
4. Vérifier les liens avec le Netcode et la sauvegarde.
5. Faire une modification limitée, puis tester la scène concernée.

Recherches utiles :

```bash
rg -n "NomDeClasse|NomDeMethode|nomDuChamp" Assets
rg -n "Assembly-CSharp::NomDeClasse" Assets/Scenes Assets/Prefabs Assets/ScriptableObjects
```

Règles Unity importantes :

- Ne pas renommer un script ou une classe Unity séparément.
- Ne pas renommer un champ public ou `[SerializeField]` sans migration
  (`[FormerlySerializedAs]` si nécessaire).
- Ne pas modifier un GUID `.meta`.
- Donner des valeurs par défaut sûres aux nouveaux champs sérialisés.
- Ne pas changer un ID persistant, un `itemId` ou un `knowledgeId` sans migration.
- Ne pas supprimer un composant référencé par une scène ou un prefab.
- Ne pas utiliser une recherche globale dans une boucle fréquente si un registre
  ou un cache peut porter la responsabilité.

## Validation minimale

Après une modification :

1. Vérifier la compilation Unity.
2. Ouvrir la scène concernée.
3. Reproduire le comportement en Play Mode.
4. Lire la première erreur de la Console avant les erreurs en cascade.
5. Pour sauvegarde, monde persistant ou Netcode, tester au minimum nouvelle
   partie, sauvegarde/chargement, host et client tardif.

La checklist détaillée se trouve dans
[Technical/Operations.md](Technical/Operations.md).

## Décisions documentaires consolidées

- La version Unity officielle du projet est `6000.4.9f1`.
- Les Chanteurs, la statue centrale, le siège de l'an 666, l'armée venue arrêter
  le rituel et les traces bleues font partie du lore canonique.
- `AgeManager` et la grille `TemporalAge` utilisent tous deux des pas de
  111 ans : `666`, `555`, `444`, `333`, `222`, `111`, `000`.
- Les idées non confirmées ne sont plus mélangées aux faits canoniques.
