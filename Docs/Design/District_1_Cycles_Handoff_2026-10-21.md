# District_1 — rapport de pause au 21 octobre 2026

> **Mise à jour du 24 octobre 2026 :** reprise autorisée. Luc est désormais
> réactivé et replacé ; le souvenir Nina est créé. La demande de proposition
> complète pour Gustave est traitée dans `Cycle_Gustave_Proposal.md`, à valider
> avant implémentation. Le reste de ce rapport décrit l'état historique du 21.
> Le bilan de reprise figure dans `District_1_Cycles_Resume_2026-10-24.md`.

Chantier suspendu à la demande de l'utilisateur, faute de crédits. L'objectif
reste la consolidation de **tous** les cycles de District_1, pas seulement les
tests techniques déjà réussis. Ne pas déclarer l'ensemble terminé.

## État livré et vérifié

- Catalogue de cycles partagé entre découverte runtime et validation éditeur ;
  Luc et Nina restent découvrables avant le chargement de leur scène.
- Progression, récompenses et mode Dev consolidés ; notamment restitution du
  reliquat des récompenses déposées partiellement au coffre, sans duplication.
- Dialogues automatiques : audience figée, fermeture naturelle obligatoire,
  interruption sans validation, reprise et protection de l'interface occupée.
- Belmont : vraie Timeline de libération de l'Ancre, liaisons, dissolution et
  révélation temporaire ; suppression de la présentation de mort concurrente.
  L'Ancient Flame réelle reste à allumer par le joueur. Wall_2 conserve sa
  condition sur cet allumage, pas sur la simple apparition de la Flame.
- Étienne et son brasero ont été recalés dans le Puits actuel : fantôme
  (-88.8, -116.24, 100.57), brasero (-88.8, -116.24, 97.52). Identifiants et
  autres transforms locaux conservés ; fichier du décor inchangé. Relecture
  sauvegardée et chemins NavMesh complets vérifiés.
- Triggers d'entrée : volume orienté réel, refus des composants/colliders
  désactivés et des coordonnées invalides, sans modifier l'autorité hôte.
- Timelines communes : contrat de bindings partagé, restauration sur échec,
  nettoyage à la perte du service, correction de la fin naturelle Unity qui
  remet le temps à zéro sous `DirectorWrapMode.None`.
- Correction réseau supplémentaire : une annulation distante pouvait remettre
  `CycleController.playback` à null pendant que sa coroutine le consultait.
  Chaque tentative conserve maintenant son handle et une génération ; une
  tentative annulée n'émet plus de résultat périmé.
- Rassemblement Ardent : contrôles hôte de portée, annulation, retrait et
  évolution de la liste des participants renforcés et testés en Edit Mode.

Les détails et les étapes intermédiaires restent dans
`Docs/Design/District_1_Cycles_Audit.md`.

## Preuves les plus récentes

| Contrôle | Résultat et preuve |
| --- | --- |
| Suite native ciblée | **43/43**, zéro ignoré : `Library/DistrictCycleValidation/CycleSequenceAndZoneNativeRetry.xml` ; hashes dans `CycleSequenceAndZoneSourceHashes.csv`. Contrats Timeline, restauration, coroutine d'annulation, rassemblement et triggers. |
| Séquences en vrais processus | **6 processus réussis** : `Run-20261021-194130/Results.csv`, dans le même dossier de validation. Hôte/client : fin normale de Timeline ; interruption client, restauration et relance ; régression du dialogue automatique. Sources dans `SourceHashes.csv`. |
| Timelines standalone | `Run-20261021-182941` : six scénarios natifs de lecture/arrêt/pause/échec et une paire hôte/client de dialogue réussis. |
| Navigation après recalage Étienne | `NavigationAudit-20261021-192230.json` : 45 points, 27 chemins complets, 10 partiels. Les deux nouveaux chemins vers Étienne et son brasero sont complets. |
| Démonstration du défaut réseau corrigé | `Run-20261021-193316` conserve l'exception d'annulation avant correction. Ne pas le confondre avec le dernier rapport vert. |

Ces contrôles isolés **ne prouvent pas** le rendu HDRP, le retour de caméra UCC,
les déplacements et combats complets ni les votes de vrais personnages en
réseau. L'ancienne suite principale de 142 tests n'a pas été intégralement
rejouée après toutes les corrections ; son rapport historique ne suffit pas.

## Demande utilisateur prioritaire non encore appliquée : Luc

L'utilisateur a explicitement autorisé : « réactive et replace ces cycles ».
La lecture native des scènes confirme que **seule la racine de Luc est
désactivée** ; Nina, Gustave, Belmont, Étienne, Ardent et Clairmont sont actives.
Ne pas déplacer les autres cycles sans anomalie établie.

La scène Luc n'a pas été modifiée pendant cette dernière intervention.

- Luc se trouve actuellement vers (-47.97, -94.29, 44.24), près de Belmont.
  Sa seule Flame proche appartient à Belmont : une dépendance à supprimer
  pour que Luc reste jouable après le déchargement de Belmont.
- La racine de la tombe est à zéro, mais son enfant visuel se trouve réellement
  vers (22.178, -89.97, 50.081), près d'une Flame permanente. Il serait faux
  d'affirmer que le visuel est à l'origine ou nécessairement hors du NavMesh.
  Le collider d'interaction n'est pas explicitement sérialisé et les outlines
  présents sont sur des objets sans renderer ; corriger ces points.
- Le collier reste vers (-7.5645, -93.0739, 55.363). Son placement doit être
  conservé, conformément au choix annoncé à l'utilisateur.
- Implantation proposée : Luc (-3.5, -93.9, 51.5), tombe (-9, -93.9, 54.8),
  sous la couverture de la Flame permanente de la chambre. Le rapport natif
  `NavigationAudit-20261021-195511.json` confirme les approches et des chemins
  complets vers la tombe (6.58 m) et le collier (5.14 m).
- Ces positions sont un **préflight**, pas un placement livré. Vérifier encore
  le sol physique, les murs, le mobilier et les dégagements avant enregistrement.
  Le premier jeu de candidats (`195139`) était rejeté : ne pas l'utiliser.
- Réactiver la racine sans recréer le contrôleur. Déplacer le fantôme ; déplacer
  la racine de tombe et neutraliser sur cette instance son grand décalage de
  modèle enfant. Ajouter un collider explicite relié à l'interaction et un
  `RuntimeOutlineTarget` sur le renderer visible ; lecture à 2 m. Garder les
  IDs réseau, la définition, les connaissances, le collier et les autres poses.
- Sauvegarder uniquement la scène Luc, après contrôle de ses modifications
  non enregistrées et sauvegarde de sécurité. Relire le résultat et ajouter
  une régression de scène/couverture lumineuse indépendante de Belmont.

L'inspection native en lecture seule est conservée dans
`Library/DistrictCycleValidation/LucPlacementAudit.json`. Bootstrap était
modifiée : aucune sauvegarde ni annulation forcée de ses changements n'a été
faite. Les outils temporaires d'inspection/correction ont été supprimés à la
pause ; aucune demande de correction n'est laissée en attente. Un Refresh
Unity à la reprise permettra de terminer leur retrait de l'éditeur.

## Reste du chantier, par priorité

1. **Terminer Luc** selon les points ci-dessus, puis contrôler le cycle complet
   : deux indices dans les deux ordres, dialogue incomplet, apaisement,
   dissolution et déchargement. Tester après Belmont déjà terminé.
2. **Rejouer la suite principale actuelle**, après enregistrement volontaire
   des scènes manuellement modifiées. Ne pas forcer la sauvegarde de Bootstrap.
   Vérifier la compilation principale après retrait des scripts temporaires.
3. **Parcours complets de chaque cycle** : Luc, Nina, Belmont, Étienne, Ardent,
   Clairmont ; inclure les départs Dev et le retour au comportement persistant.
   Classer les chemins NavMesh partiels par inspection réelle, pas par rebake
   aveugle. Vérifier les collisions, portes, BattleWall et accès au frein/relais.
4. **Cinématiques dans les scènes réelles** : cadrages, caméras Cinemachine,
   verrous UCC, silhouettes, contrepoids, fin normale, interruption,
   déchargement et liaison manquante. Rien ne doit rester verrouillé ni être
   validé sur échec ; les souvenirs doivent être rejouables après un échec.
5. **Multijoueur avec personnages complets** : votes d'Ardent, sortie du rayon,
   annulation, cooldown, interactions simultanées, déconnexion et arrivée
   tardive. Puis boss, torches, frein, dégâts et étapes de chaque cycle. Les
   essais réseau livrés couvrent le protocole de présentation, pas tout cela.
6. **Sauvegarde/rechargement et réentrée** à chaque jalon ; préserver les
   preuves, connaissances, récompenses et accès après déchargement. Vérifier
   que le mode Dev ne laisse aucune écriture réelle. Après recharge en combat,
   les boss concernés doivent revenir vivants à pleine santé sans perdre
   l'enquête déjà acquise.
7. **Deux décisions de contenu encore requises** : Gustave n'a qu'une note,
   sans véritable définition/contrôleur de cycle ; confirmer si cela reste un
   indice ou devient une quête. Nina possède une étape de souvenir facultative
   sans Timeline ni profil : choisir une cinématique ou nettoyer ce contenu
   inachevé en conservant les identifiants de sauvegarde. Ne pas inventer une
   résolution canonique ou supprimer ces éléments sans accord.
8. **Clôture** : remettre à jour une matrice d'acceptation par cycle et ne fermer
   l'objectif qu'après preuves des parcours solo, réseau, persistance et rendu.

## Protections de reprise

- Le worktree est fortement modifié, notamment par d'autres travaux sur la
  persistance et les animations. Préserver les modifications étrangères.
- Ne lancer aucun générateur de reconstruction de décor ou de cycles.
- Préserver Belmont, le Puits, les prefabs sources, Juggernaut, les GUID et
  identifiants persistants. Faire des changements ciblés avec snapshots.
- Les outils reproductibles restent sous `Tools/Validation/CycleNetworkSmoke`
  et `Tools/Validation/DistrictNavigation`, hors des scripts de production.
  Ne pas lancer deux validations simultanément dans leur projet isolé commun.
- Ne pas arrêter les instances Unity d'autres projets de validation : elles
  appartiennent à d'autres travaux. Aucun processus de validation de cette
  intervention n'est encore à attendre.
