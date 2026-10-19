# Clairmont — Le registre ouvert

## Intention et lore

Après les Conduits Noyés, la Nef des Tours Interrompus montre où convergent les
circulations de Veillée. Agnès Clairmont, préposée au registre, est morte pendant
la catastrophe de 666. Elle attend encore des retours pour fermer son registre.

Deux traces et la lecture du tableau montrent que ses colonnes comptaient les
transmissions au poste suivant, pas les retours aux quartiers. La relève avait
bien accusé réception. Le joueur lui permet de cesser de se croire responsable
d'un nom oublié. Le cycle n'établit pas le sort des groupes après leur départ.

Les rainures continuent sous des inscriptions plus récentes : le trajet vers les
soubassements de la statue précède les mots utilisés par les derniers habitants.
Aucune révélation des fondateurs, du rituel ou du destin final d'Iris.

Ce cycle privilégie l'enquête et le parcours, sans nouveau boss ni cinématique
obligatoire. Il n'ajoute pas une nouvelle Ancient Flame ni une liste de collectibles.

## Scènes

- `District_1_NefDesToursInterrompus_Environment` : décor, parcours, Flames,
  raccourci de maintenance et accès persistant au belvédère.
- `District_1_Cycle_Clairmont` : Agnès, deux preuves, tableau, trigger et contrôleur.
- `District_1_AbilityShortcuts` : raccourci permanent du couloir ouest initial.

Toutes appartiennent au manifeste District_1. Définition sous
`Assets/Resources/Narrative/ClairmontCycle.asset`, identifiant `district1.clairmont`.
Les autres assets de quête sont sous `Assets/Scenes/Cycles/Cycle_Clairmont`.

Le raccord suit l'ancienne `Nef_FutureLimit`, à (-90.50, -116.24, 190.52), dans
le prolongement +Z du corridor existant. Cette seule limite est archivée inactive.
La porte persistante Ardent `Processions_Blocker` reste en place et conserve sa
condition. Aucun déplacement du Puits, de Belmont ou des acteurs Ardent.

La nef est une base éditable : modules Dungeon_Environment, galerie périphérique
autour d'une masse de maçonnerie ancienne, deux postes de travail et belvédère.
Les matériaux incompatibles sont adaptés localement, sans modifier le pack.

## Parcours

1. `nef_entered` : entrée physique dans la nef, puis réplique de Lucian.
2. `relief_register_read` et `old_route_read` : deux preuves parallèles,
   récupérables et lisibles à 2 mètres.
3. `register_consulted` : consulter le tableau après les deux preuves ; connaissance
   du sens des colonnes. Avant cela, Lucian indique ce qui manque.
4. `agnes_spoken` : résolution volontaire d'Agnès avec les trois connaissances.
   Dissolution et activation `district1.clairmont.statue_belvedere_access`.
5. `lucian_after_clairmont` : réplique finale, seule étape terminale.

Le cycle est indépendant : aucun prérequis de complétion d'Ardent dans sa
définition. La porte physique précédente organise la chronologie.

## Raccourcis et capacités

Chaque raccourci a deux déclencheurs mais un seul TrouEtroit et une seule
découverte persistante. Les bouches de maintenance restent visibles ; Observateur
révèle le passage, Sauf-Conduit équipé permet de le traverser. Les messages locaux
expliquent la capacité manquante ou le retour à la Maison pour changer.

- **Nef** : traverse la maçonnerie centrale, entre le seuil et l'arrière de la nef,
  en évitant la galerie périphérique. N'ouvre pas la grille du belvédère.
- **District initial** : coupe le détour du couloir ouest. Il est visible avec
  Observateur avant Belmont, mais Sauf-Conduit reste verrouillé jusqu'à sa fin.
  Ne traverse ni Wall_1, ni Wall_2, ni la porte Étienne, ni la porte Ardent.

Placements finaux contrôlés par navigation :

| Passage | Extrémité A (monde) | Extrémité B (monde) | Marche ordinaire / passage |
|---|---|---|---|
| Nef | (-90.50, -116.157, 206.52) | (-90.50, -116.157, 230.52) | 33,51 m / 24 m |
| Couloir ouest | (-29, -94.123, 46) | (-49, -93.733, 38) | 36,01 m / 21,54 m |

Le premier placement envisagé du raccourci initial a été rejeté : son chemin
ordinaire n'était pas continu. Les positions finales appartiennent à un même
parcours accessible sans ouvrir de porte narrative pendant le contrôle.

En solo, une découverte est conservée après le changement de capacité à la Maison.
En coopération, un Observateur peut révéler le passage ; seuls les joueurs ayant
Sauf-Conduit équipé le traversent. Le parcours ordinaire reste disponible à tous.

## Validation

Menu `Lit > Validation > Valider Clairmont et les raccourcis`, tests
`ClairmontCycleTests`. Départs Dev utiles : `relief_register_read`,
`register_consulted`, `agnes_spoken`.

Les rapports de création et navigation sont locaux sous
`Library/ClairmontAuthoring`. Aucune session hôte/client n'est présumée.

### Navigation exécutée le 19 octobre 2026

NavMesh complet de District_1 rebâti dans
`Assets/Navigation/NavMeshData/District_1_Clairmont_NavMeshData.asset` et assigné au
manifeste. Chemins complets vérifiés vers le raccord Ardent, les deux preuves,
le tableau, Agnès, la sortie de nef et le belvédère. Pour ce dernier contrôle,
la nouvelle grille seule a été temporairement désactivée, sans écrire son
activation ; elle a ensuite été réactivée et enregistrée fermée.

L'audit de données et **sept cas de test exécutés dans Unity** passent :
indépendance, permutations des preuves, Dev sans écriture, deux extrémités et
destinations bidirectionnelles ; tests isolés de l'autorité pour les deux passages,
refus avant déblocage, refus sans découverte, refus avec Observateur seul, changement
autorisé à la Maison et refusé ailleurs, acceptation aux deux extrémités avec
Sauf-Conduit équipé, refus hors de portée. Les quinze contrôles AbilitySO passent.

Les outils temporaires de création ont été retirés. Les scènes et prefabs restent
éditables ; seul l'audit et les tests permanents sont conservés. La scène originale
des corridors est identique au snapshot avant intervention. Les modifications des
Conduits Noyés sont limitées à l'archivage de l'ancienne limite de la nef.

À vérifier par un parcours complet avec les vrais personnages : ressenti des
interactions et des transitions, caméra, lecture de l'enquête et hôte/client.
Les tests isolés ne constituent pas une session multijoueur complète.
