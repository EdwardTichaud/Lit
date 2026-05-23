# Lit - Données narratives

Ce document décrit les structures de données légères à utiliser pour soutenir la
vision actuelle sans créer de système lourd.

## Readable Items

Les `Item` ScriptableObjects restent la base des documents lisibles. Les nouveaux
champs de métadonnées narratives servent à classer un readable sans casser les
assets existants :

- âge associé ;
- quartier ;
- lignée ;
- courant religieux ;
- niveau de révélation ;
- tags narratifs.

Ces champs sont optionnels. Un vieux readable sans métadonnées reste valide.

Un readable peut aussi débloquer des connaissances via
`Item.knowledgeUnlockedOnRead`. L'ouverture du document suffit pour le premier
système : les raffinements "page précise lue" ou "lecture complète" pourront être
ajoutés plus tard si le besoin gameplay devient réel.

## Knowledge-driven narrative

Les `KnowledgeSO` sont les faits persistants que le joueur a réellement appris.
Ils remplacent progressivement les réponses libres et les mots de passe.

Champs utiles :

- `knowledgeId` : identifiant stable pour gameplay et sauvegarde ;
- `title` / `description` : affichage sobre ou debug ;
- `category` : habitant, lignée, registre, chambre, brasero, vérité, etc. ;
- `sourceType` : readable, registre, objet, lieu, fantôme, contradiction ;
- `tags` : liens implicites pour des requirements simples ;
- liens narratifs : quartier, chambre, personne, lignée, objet, readable, âge.

La possession des connaissances reste dans `KnowledgeManager`, déjà persisté par
`PersistentKnowledgeState`. Les nouveaux systèmes doivent donc vérifier
`KnowledgeManager.HasKnowledge`, `HasKnowledgeInCategory`, `HasKnowledgeWithTag`,
`CountKnowledgeInCategory` ou `CountKnowledgeWithTag` au lieu de créer un état
séparé.

## Connaissances implicites

Les déductions combinées restent intégrées à `KnowledgeRequirement` :

- `requiredKnowledge` : tous ces faits précis doivent être connus ;
- `anyKnowledge` : au moins un fait de la liste doit être connu ;
- `requiredCategories` : au moins une connaissance dans chaque catégorie ;
- `requiredTags` : au moins une connaissance pour chaque tag ;
- `requiredCategoryCounts` : au moins N connaissances dans une catégorie ;
- `requiredTagCounts` : au moins N connaissances portant un tag.

Exemple : une réaction de fantôme peut demander trois connaissances portant le
tag `quartier_lune_pleine` pour signifier que le joueur comprend assez bien ce
quartier, sans créer un asset "méta-connaissance" dédié.

## Déblocage de connaissances

Sources actuelles :

- `Item.knowledgeUnlockedOnRead` pour les livres et parchemins ;
- `KnowledgeUnlockTrigger` pour observation, entrée dans un lieu, anomalie ou
  événement de scène ;
- `LocalVoiceLineController` pour débloquer une connaissance après une ligne de
  voix ;
- `GhostController` pour débloquer une connaissance à l'écoute ou après une
  réaction réussie.

TODO : ajouter plus tard un déblocage par page de readable ou par entrée précise
de registre si le système de lecture en a réellement besoin.

## Fantômes et connaissances

Un `GhostData` décrit désormais une question fixe et des `GhostKnowledgeReaction`.
Chaque réaction possède une `KnowledgeRequirement` et peut débloquer de nouvelles
connaissances. Le joueur ne tape plus de phrase : le fantôme réagit à ce que le
joueur sait déjà.

Si plusieurs réactions sont disponibles, `GhostController` peut afficher une
liste d'options issues des connaissances du joueur. Si une seule réaction est
disponible, elle peut être jouée directement pour garder le rythme.

Le `GhostController` pilote aussi un dissolve de proximité : tant qu'aucun
personnage contrôlé n'est dans la zone, le fantôme reste au dissolve amount max.
Quand le personnage approche, le dissolve lerp vers `0` en `1` seconde par
défaut. La liste `Proximity Dissolve Targets` permet de viser un ou plusieurs
GameObjects de rendu ; vide, le GameObject du contrôleur est utilisé.

Une réaction peut aussi déclencher un effet de scène par identifiant. Le
ScriptableObject garde seulement des `triggerEffectIds`, tandis que le
`GhostController` de la scène contient les `dissolveEffectRules` et la liste des
GameObjects à dissoudre. Cela évite de stocker des références de scène dans
`GhostData`.

Exemple actuel : `GhostData_Luc` possède une réaction qui demande
`Knowledge_JonLocation` et déclenche l'effet `luc_dissolve`. Dans `Maison`, le
`GhostController` de Luc mappe cet identifiant vers une liste de GameObjects
cibles.

L'état runtime "fantôme compris" est séparé des connaissances et persiste via
`PersistentGhostState`. Les faits appris restent dans `KnowledgeManager`.

Les anciens puzzles de saisie (`ReadableSentencePuzzle`) restent disponibles pour
les scènes existantes, mais doivent être considérés comme legacy.

## Registres

Créer des `RegistryEntry` pour les entrées importantes seulement. Il n'est pas
nécessaire de modéliser chaque habitant du château.

Types prioritaires :

- naissance ;
- décès ;
- déplacement ;
- habitation ;
- correction ;
- veille ;
- ration ;
- entretien.

Une entrée peut pointer vers un readable ou un objet associé quand cela aide le
level design.

## Lignées

Créer des `FamilyRecord` quand un personnage ou une famille sert une enquête.

Statuts utiles :

- normal ;
- rayé ;
- déplacé ;
- disparu ;
- non renseigné.

Les parents, enfants et conjoints peuvent être renseignés par identifiants texte
au début. Ne pas construire un arbre généalogique complet avant d'avoir un besoin
clair en gameplay.

## Objets transgénérationnels

Créer un `TransgenerationalObjectRecord` pour les objets qui reviennent à plusieurs
âges : pendentif, couverture, jouet, livre, bol, outil.

Un bon objet transmis doit pouvoir répondre à trois questions :

- qui l'a possédé ;
- où le joueur le retrouve ;
- ce que son état révèle sur le passage du temps.

## Transformations humaines

Les modifications humaines peuvent rester de simples tags sur un `TemporalObject`
ou dans une note de registre.

Tags recommandés :

- mur condamné ;
- chambre réaffectée ;
- lit ajouté ;
- fenêtre murée ;
- brasero déplacé ;
- passage fermé ;
- registre corrigé.

Ne créer une classe plus complexe que si plusieurs systèmes doivent réellement
interroger ces transformations.
