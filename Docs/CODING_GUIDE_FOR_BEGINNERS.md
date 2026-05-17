# Lit - Guide de code pour débutants

Ce guide explique comment modifier le projet sans casser Unity, les scènes ou les sauvegardes.

## Règles de base

1. Ne modifie jamais `Packages/`, `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/` ou `UserSettings/`.
2. Ne modifie pas les assets tiers dans `Assets/0 - UnityPackages/`, `Assets/TextMesh Pro/`, `Assets/Sketchfab For Unity/`, `Assets/QuickOutline/` ou `Assets/TutorialInfo/`.
3. Ne modifie pas `Assets/PlayerInputs.cs` : il est généré automatiquement.
4. Ne renomme pas une classe Unity sans renommer le fichier de la même manière.
5. Ne renomme pas un champ public ou `[SerializeField]` sans savoir où il est utilisé dans les scènes/prefabs.
6. Fais une petite modification, teste, puis continue.

## Conventions observées

### MonoBehaviour

Un `MonoBehaviour` est un script attaché à un GameObject dans une scène ou un prefab.

Exemples :

- `Door`
- `Brasero`
- `InteractableItem`
- `SquadCharacterController`
- `CombatSessionManager`

À vérifier avant modification :

- le script est-il attaché dans une scène ?
- ses champs sont-ils remplis dans l'inspecteur ?
- est-il utilisé par un autre manager ?
- est-il synchronisé en Netcode ?

### ScriptableObject

Un `ScriptableObject` est une donnée éditable comme un item, un personnage ou un son.

Exemples :

- `Item`
- `CharacterData`
- `AudioClipSO`
- `TorchVisionDefinition`
- `ZoneAudioProfileSO`

À vérifier avant modification :

- des assets `.asset` existent-ils déjà ?
- le champ ajouté doit-il rester optionnel ?
- un ID stable est-il utilisé par la sauvegarde ou le réseau ?

### Managers

Un manager coordonne un système entier.

Exemples :

- `SquadManager`
- `AudioManager`
- `CombatSessionManager`
- `WorldStateManager`
- `SaveSessionManager`

Les managers sont souvent à risque élevé car beaucoup d'autres scripts les appellent.

## Comment ajouter une fonctionnalité proprement

1. Décris le besoin en une phrase.
2. Cherche si un système proche existe déjà avec `rg`.
3. Ajoute la logique dans le système existant si c'est naturel.
4. Si un nouveau script est nécessaire, garde-le petit et spécialisé.
5. Ajoute des champs `[SerializeField] private` plutôt que `public` quand ils servent seulement à l'inspecteur.
6. Donne une valeur par défaut sûre aux nouveaux champs.
7. Ne force pas une scène existante à être modifiée si le code peut rester optionnel.
8. Teste en Play Mode.

Exemple de recherche :

```bash
rg -n "InventoryPanelController|NetworkInventory|Item" Assets/Scripts Assets/ScriptableObjects
```

## Comment modifier un script sans casser le jeu

### Avant

- Lire le fichier entier si possible.
- Chercher les références :

```bash
rg -n "NomDeClasse|NomDeMethode|nomDuChamp" Assets
```

- Vérifier si la classe apparaît dans des scènes ou prefabs :

```bash
rg -n "Assembly-CSharp::NomDeClasse" Assets/Scenes Assets/Prefabs Assets/ScriptableObjects
```

### Pendant

- Ne change pas les signatures publiques sans raison.
- Ne change pas les noms de champs sérialisés.
- Ajoute un commentaire si l'intention est ambiguë.
- Garde les changements groupés par système.

### Après

- Sauvegarde le fichier.
- Laisse Unity recompiler.
- Corrige les erreurs avant de continuer.
- Teste la scène concernée.
- Si Git est disponible, fais un commit ciblé.

## Comment tester une modification

### Test minimal

1. Ouvrir la scène concernée.
2. Entrer en Play Mode.
3. Reproduire l'action touchée par le code.
4. Regarder la Console Unity.
5. Quitter Play Mode et vérifier qu'aucune valeur importante n'a été écrasée.

### Tests par système

- Mouvement : marcher, courir, sauter, monter une échelle, changer de personnage.
- Interaction : utiliser porte, levier, brasero, item, readable.
- Inventaire : ramasser, utiliser, dropper, lire, crafter.
- UI : ouvrir/fermer menus, naviguer au clavier/manette, confirmer/annuler.
- Sauvegarde : nouvelle partie, sauvegarder, quitter, recharger.
- Netcode : host, client, switch personnage, inventaire, interaction serveur.
- Combat : déclencher, attaquer, utiliser item, victoire, défaite.

## Comment lire une erreur Unity

Une erreur de compilation indique souvent :

- fichier ;
- ligne ;
- type d'erreur ;
- symbole introuvable ou conversion impossible.

Commence toujours par la première erreur, pas la dernière. Une seule erreur peut en créer beaucoup d'autres.

## Glossaire Unity/C# adapté au projet

**GameObject** : objet dans une scène Unity.

**Component** : bloc attaché à un GameObject. Un script `MonoBehaviour` est un component.

**Prefab** : modèle réutilisable d'objet.

**Scene** : niveau ou écran Unity.

**Inspector** : panneau Unity où l'on configure les champs sérialisés.

**SerializeField** : permet de voir un champ privé dans l'inspecteur.

**ScriptableObject** : asset de données réutilisable.

**Awake** : appelé quand l'objet est chargé. Sert souvent à initialiser des références internes.

**Start** : appelé avant la première frame, après `Awake`.

**OnEnable / OnDisable** : appelés quand un component devient actif/inactif. Souvent utilisés pour s'abonner ou se désabonner à des événements.

**Update** : appelé chaque frame. À utiliser avec prudence.

**FixedUpdate** : appelé au rythme physique. Utile pour Rigidbody et physique.

**OnTriggerEnter / OnTriggerExit** : appelés quand un collider entre/sort d'un trigger.

**RPC** : appel réseau utilisé par Netcode pour demander ou diffuser une action.

**Host autoritaire** : en multijoueur, le host décide de l'état réel du jeu.

**GUID Unity** : identifiant dans les `.meta`. Ne pas modifier.

**Persistent ID** : ID stable utilisé par la sauvegarde ou la reconstruction réseau.

## Ce qu'il vaut mieux éviter

- Ajouter un singleton global sans vérifier s'il existe déjà un manager.
- Utiliser `FindObjectOfType` partout pour régler vite un problème.
- Modifier un prefab ou une scène importée d'un asset tiers.
- Faire une refonte en même temps qu'un bugfix.
- Ajouter une dépendance lourde pour une petite donnée.
- Corriger un système réseau sans test host/client.

## Bonne méthode pour progresser sans IA

1. Apprendre à chercher dans le projet avec `rg`.
2. Savoir reconnaître un `MonoBehaviour`, un `ScriptableObject` et un prefab.
3. Comprendre les méthodes Unity (`Awake`, `Start`, `Update`, triggers).
4. Comprendre les champs sérialisés et pourquoi les renommer est dangereux.
5. Lire les erreurs de compilation.
6. Utiliser Git pour faire de petits commits.
7. Tester une scène après chaque changement.
